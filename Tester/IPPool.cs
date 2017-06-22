using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AttemptController.EncryptionPrimitives;

namespace Tester
{
    public class IpPool
    {
        private IPAddress _currentProxyAddress = null;
        private int _numberOfClientsBehindTheCurrentProxy = 0;
        private readonly ConcurrentBag<IPAddress> _ipAddresssesInUseByBenignUsers = new ConcurrentBag<IPAddress>();
        private readonly ConcurrentDictionary<IPAddress, IpAddressDebugInfo> _debugInformationAboutIpAddresses = new ConcurrentDictionary<IPAddress, IpAddressDebugInfo>();
        private readonly ExperimentalConfiguration _experimentalConfiguration;


        public IpPool(ExperimentalConfiguration experimentalConfiguration)
        {
            _experimentalConfiguration = experimentalConfiguration;
        }


        public IpAddressDebugInfo GetIpAddressDebugInfo(IPAddress address)
        {
            return _debugInformationAboutIpAddresses.GetOrAdd(address, a => new IpAddressDebugInfo());
        }

        private readonly Object _proxyAddressLock = new object();
        public IPAddress GetNewRandomBenignIp()
        {
            IpAddressDebugInfo debugInfo;
            if (StrongRandomNumberGenerator.GetFraction() < _experimentalConfiguration.FractionOfBenignIPsBehindProxies)
            {
                lock (_proxyAddressLock)
                {
                    if (_currentProxyAddress == null || ++_numberOfClientsBehindTheCurrentProxy >=
                        _experimentalConfiguration.ProxySizeInUniqueClientIPs)
                    {
                        _currentProxyAddress = new IPAddress(StrongRandomNumberGenerator.Get32Bits());
                        debugInfo = GetIpAddressDebugInfo(_currentProxyAddress);
                        debugInfo.IsPartOfProxy = true;
                        debugInfo.UsedByBenignUsers = true;
                        _numberOfClientsBehindTheCurrentProxy = 0;
                        return _currentProxyAddress;
                    }
                    else
                    {
                        return _currentProxyAddress;
                    }
                }
            }
            else
            {
                IPAddress address = new IPAddress(StrongRandomNumberGenerator.Get32Bits());
                _ipAddresssesInUseByBenignUsers.Add(address);
                debugInfo = GetIpAddressDebugInfo(address);
                debugInfo.UsedByBenignUsers = true;
                return address;
            }
        }


        private readonly List<IPAddress> _maliciousIpAddresses = new List<IPAddress>();
        public void GenerateAttackersIps()
        {
            List<IPAddress> listOfIpAddressesInUseByBenignUsers = _ipAddresssesInUseByBenignUsers.ToList();
            uint numberOfOverlappingIps = (uint)
                (_experimentalConfiguration.NumberOfIpAddressesControlledByAttacker *
                 _experimentalConfiguration.FractionOfMaliciousIPsToOverlapWithBenign);
            uint i;
            for (i = 0; i < numberOfOverlappingIps && listOfIpAddressesInUseByBenignUsers.Count > 0; i++)
            {
                int randIndex = (int)StrongRandomNumberGenerator.Get32Bits(listOfIpAddressesInUseByBenignUsers.Count);
                IPAddress address = listOfIpAddressesInUseByBenignUsers[randIndex];
                IpAddressDebugInfo debugInfo = GetIpAddressDebugInfo(address);
                lock (debugInfo)
                {
                    debugInfo.UsedByAttackers = true;
                }
                _maliciousIpAddresses.Add(address);
                listOfIpAddressesInUseByBenignUsers.RemoveAt(randIndex);
            }
            for (; i < _experimentalConfiguration.NumberOfIpAddressesControlledByAttacker; i++)
            {
                IPAddress address = new IPAddress(StrongRandomNumberGenerator.Get32Bits());
                IpAddressDebugInfo debugInfo = GetIpAddressDebugInfo(address);
                lock (debugInfo)
                {
                    debugInfo.UsedByAttackers = true;
                }
                _maliciousIpAddresses.Add(address);
            }
        }


        public IPAddress GetRandomMaliciousIp()
        {
            int randIndex = (int)StrongRandomNumberGenerator.Get32Bits(_maliciousIpAddresses.Count);
            IPAddress address = _maliciousIpAddresses[randIndex];
            return _maliciousIpAddresses[randIndex];
        }

    }
}
