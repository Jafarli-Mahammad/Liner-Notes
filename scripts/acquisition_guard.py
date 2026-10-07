"""Offline-testable accounting. Does not acquire data or apply a retention policy."""
from dataclasses import dataclass
from threading import Lock

CATEGORIES = frozenset({"recordings", "http_cache", "derived_database", "reports", "copies", "backups", "partials"})
SHARED_STOP_BYTES = 80_000_000


class AcquisitionStopped(RuntimeError):
    pass


def integer(value, name, minimum=0):
    if type(value) is not int or value < minimum or value > 2**63 - 1:
        raise AcquisitionStopped(f"Invalid {name}")
    return value


@dataclass(frozen=True)
class Inventory:
    categories: dict
    revision: str
    reconciled: bool

    def total(self):
        if not self.reconciled or not self.revision or set(self.categories) != CATEGORIES:
            raise AcquisitionStopped("Unknown or stale shared inventory")
        total = sum(integer(value, "inventory bytes") for value in self.categories.values())
        return integer(total, "aggregate inventory")


class AcquisitionGuard:
    """One in-process lease plus reservations. Recorder also takes an OS writer lock."""

    def __init__(self, inventory, run_cap, response_cap, shared_cap=SHARED_STOP_BYTES):
        self.inventory = Inventory(dict(inventory.categories), inventory.revision, inventory.reconciled)
        self.used = self.inventory.total()
        self.run_cap = integer(run_cap, "run cap", 1)
        self.response_cap = integer(response_cap, "response cap", 1)
        self.shared_cap = integer(shared_cap, "shared cap", 1)
        if self.shared_cap > SHARED_STOP_BYTES:
            raise AcquisitionStopped("Cannot raise the shared threshold")
        self.run_used = 0
        self.lock = Lock()

    def reserve(self, max_response, overhead, revision, cancelled=False):
        if cancelled:
            raise AcquisitionStopped("Cancelled")
        if revision != self.inventory.revision:
            raise AcquisitionStopped("External inventory change")
        response = integer(max_response, "response reservation")
        metadata = integer(overhead, "metadata reservation")
        if response > self.response_cap:
            raise AcquisitionStopped("Response reservation exceeds bound")
        amount = integer(response + metadata, "reservation")
        if not self.lock.acquire(blocking=False):
            raise AcquisitionStopped("Concurrent acquisition")
        try:
            if self.used + amount >= self.shared_cap or self.run_used + amount > self.run_cap:
                raise AcquisitionStopped("Projected storage limit reached")
            return Reservation(self, response, metadata)
        except BaseException:
            self.lock.release()
            raise


class Reservation:
    def __init__(self, guard, response, overhead):
        self.guard, self.response, self.overhead = guard, response, overhead
        self.closed = False

    def commit(self, response_bytes, metadata_bytes, new_revision, cancelled=False):
        if self.closed:
            raise AcquisitionStopped("Reservation already closed")
        try:
            integer(response_bytes, "response bytes")
            integer(metadata_bytes, "metadata bytes")
            if cancelled or response_bytes > self.response or metadata_bytes > self.overhead or not new_revision:
                raise AcquisitionStopped("Cancelled or oversized response/output")
            self.guard.used += response_bytes + metadata_bytes
            self.guard.run_used += response_bytes + metadata_bytes
            categories = dict(self.guard.inventory.categories)
            categories["recordings"] += response_bytes + metadata_bytes
            self.guard.inventory = Inventory(categories, new_revision, True)
        finally:
            self.close()

    def close(self):
        if not self.closed:
            self.closed = True
            self.guard.lock.release()

    def __enter__(self):
        return self

    def __exit__(self, *unused):
        self.close()
