import copy
import unittest

from production_topology import resolve, validate


class ProductionTopologyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.config = resolve()

    def test_production_and_loopback_topologies(self):
        validate(self.config)
        validate(resolve(True), True)

    def test_database_exposure_is_rejected(self):
        config = copy.deepcopy(self.config)
        config['services']['db']['ports'] = [{'target': 5432, 'published': '5432'}]
        with self.assertRaisesRegex(ValueError, 'private_service_port_published'):
            validate(config)

    def test_development_and_unbounded_resources_are_rejected(self):
        config = copy.deepcopy(self.config)
        config['services']['web']['environment']['Generation__Origin'] = 'Synthetic'
        with self.assertRaisesRegex(ValueError, 'development_runtime_in_production'):
            validate(config)

    def test_split_tmpfs_options_are_rejected(self):
        config = copy.deepcopy(self.config)
        config['services']['web']['tmpfs'] = ['/tmp:rw','noexec','nosuid','size=64m']
        with self.assertRaisesRegex(ValueError,'runtime_temporary_mount_invalid'):
            validate(config)

    def test_web_cannot_take_reserved_proxy_address(self):
        config=copy.deepcopy(self.config)
        config['services']['web']['networks']['frontend']['ipv4_address']=config['services']['proxy']['networks']['frontend']['ipv4_address']
        with self.assertRaisesRegex(ValueError,'proxy_and_web_addresses_must_be_distinct'): validate(config)
        config = copy.deepcopy(self.config)
        config['services']['db'].pop('mem_limit')
        with self.assertRaisesRegex(ValueError, 'service_resources_unbounded'):
            validate(config)


if __name__ == '__main__':
    unittest.main()
