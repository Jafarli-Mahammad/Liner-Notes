import json
import unittest
from unittest.mock import patch
from production_recovery import available_networks


class RecoveryNetworkTests(unittest.TestCase):
    def test_ignores_unconfigured_networks_and_avoids_existing_ranges(self):
        networks=[{'IPAM':{'Config':None}},{'IPAM':{'Config':[{'Subnet':'172.30.100.0/24'}]}}]
        with patch('subprocess.check_output',side_effect=[b'first\nsecond\n',json.dumps(networks).encode()]):
            self.assertEqual(['172.30.101.0/28','172.30.102.0/28'],available_networks())
