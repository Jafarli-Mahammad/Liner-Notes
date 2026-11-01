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
        config = copy.deepcopy(self.config)
        config['services']['db'].pop('mem_limit')
        with self.assertRaisesRegex(ValueError, 'service_resources_unbounded'):
            validate(config)


if __name__ == '__main__':
    unittest.main()
