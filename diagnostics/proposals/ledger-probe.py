#!/usr/bin/env python3
"""Disposable SQL primitive experiment, never an application migration.

Requires the remediation-owned container name and runs only in a new dedicated database.
No production connection settings or credentials are accepted or printed.
"""
import argparse
import json
import pathlib
import statistics
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("--container", required=True)
parser.add_argument("--output", type=pathlib.Path, required=True)
args = parser.parse_args()
if not args.container.startswith("liner-audit-"):
    raise SystemExit("Only the remediation-owned disposable container is supported")
database = "liner_design_20261029"


def sql(text, db=database):
    result = subprocess.run(["docker", "exec", "-i", args.container, "psql", "-X", "-qAt",
                             "-U", "postgres", "-d", db, "-v", "ON_ERROR_STOP=1"],
                            input=text, capture_output=True, text=True, check=True)
    return result.stdout.strip()


sql(f"CREATE DATABASE {database}", "template1")
try:
    sql("""
      CREATE TABLE ledger(id int PRIMARY KEY, bytes bigint, rows bigint, revision bigint);
      INSERT INTO ledger VALUES(1,0,0,1);
      CREATE TABLE items(id int PRIMARY KEY, payload text);
      CREATE FUNCTION row_delta() RETURNS trigger LANGUAGE plpgsql AS $$
      BEGIN
        UPDATE ledger SET bytes=bytes+octet_length(to_jsonb(NEW)::text)+512,rows=rows+1,revision=revision+1 WHERE id=1;
        IF NOT FOUND THEN RAISE EXCEPTION 'missing ledger'; END IF;
        RETURN NULL;
      END $$;
      CREATE FUNCTION statement_delta() RETURNS trigger LANGUAGE plpgsql AS $$
      DECLARE n bigint; b bigint;
      BEGIN
        SELECT count(*),coalesce(sum(octet_length(to_jsonb(t)::text)+512),0) INTO n,b FROM inserted t;
        IF n>0 THEN
          UPDATE ledger SET bytes=bytes+b,rows=rows+n,revision=revision+n WHERE id=1;
          IF NOT FOUND THEN RAISE EXCEPTION 'missing ledger'; END IF;
        END IF;
        RETURN NULL;
      END $$;
    """)
    results = []
    for count in [100, 1000, 10000]:
        for mode in ["row", "statement"]:
            sql("DROP TRIGGER IF EXISTS delta ON items;" +
                ("CREATE TRIGGER delta AFTER INSERT ON items FOR EACH ROW EXECUTE FUNCTION row_delta();"
                 if mode == "row" else
                 "CREATE TRIGGER delta AFTER INSERT ON items REFERENCING NEW TABLE AS inserted FOR EACH STATEMENT EXECUTE FUNCTION statement_delta();"))
            samples = []
            for sample in range(21):
                raw = sql(f"""BEGIN;
                  EXPLAIN (ANALYZE,BUFFERS,WAL,FORMAT JSON) INSERT INTO items SELECT n,repeat('x',256) FROM generate_series(1,{count}) n;
                  SELECT json_build_object('bytes',l.bytes,'rows',l.rows,'revision',l.revision,
                    'expectedBytes',(SELECT sum(octet_length(to_jsonb(t)::text)+512) FROM items t)) FROM ledger l;
                  ROLLBACK;""")
                decoder = json.JSONDecoder(); plan, end = decoder.raw_decode(raw)
                accounting = json.loads(raw[end:].strip())
                assert accounting["bytes"] == accounting["expectedBytes"]
                assert accounting["rows"] == count and accounting["revision"] == count+1
                assert sql("SELECT bytes||','||rows||','||revision FROM ledger") == "0,0,1"
                if sample:
                    samples.append({"ms": plan[0]["Execution Time"],
                                    "walBytes": plan[0]["Plan"].get("WAL Bytes"),
                                    "sharedHitBlocks": plan[0]["Plan"].get("Shared Hit Blocks")})
            values = sorted(x["ms"] for x in samples)
            results.append({"mode": mode, "rows": count, "samples": samples,
                            "p50Ms": statistics.median(values), "p95Ms": values[18],
                            "exactInsertAccountingAndRollback": True})
    args.output.write_text(json.dumps({"experiment": "insert-only-ledger-primitive",
        "limits": "Not a full replacement: update/delete/truncate/cascades/partition/upsert/concurrency/schema-tamper coverage remains mandatory.",
        "results": results}, indent=2))
finally:
    sql(f"DROP DATABASE {database}", "template1")
