using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;
using AttemptController.Interfaces;
using AttemptController.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AttemptController.Clients
{

    public class DistributedBinomialLadderFilterClient : IBinomialLadderFilter
    {

        public const string ControllerPath = "/api/DBLS/";

        public const string BitsPath = ControllerPath + "Bits/";

        public const string ElementsPath = ControllerPath + "Elements/";

        public readonly int NumberOfShards;

        public readonly int MaxLadderHeight;

        public readonly TimeSpan MinimumCacheFreshnessRequired;

        protected UniversalHashFunction ShardHashFunction;

        public IDistributedResponsibilitySet<RemoteHost> ShardToHostMapping;

        protected FixedSizeLruCache<string, DateTime> CacheOfElementsAtTopOfLadder;

        public DistributedBinomialLadderFilterClient(int numberOfShards, int defaultHeightOfLadder, IDistributedResponsibilitySet<RemoteHost> shardToHostMapping, string configurationKey, TimeSpan? mininmumCacheFreshnessRequired = null)
        {
            NumberOfShards = numberOfShards;
            MaxLadderHeight = defaultHeightOfLadder;
            MinimumCacheFreshnessRequired = mininmumCacheFreshnessRequired ?? new TimeSpan(0,0,1);
            CacheOfElementsAtTopOfLadder = new FixedSizeLruCache<string, DateTime>(2*NumberOfShards);
            ShardHashFunction = new UniversalHashFunction(configurationKey);
            ShardToHostMapping = shardToHostMapping;
        }


        public int GetShardIndex(string key)
            => (int)(ShardHashFunction.Hash(key) % (uint)NumberOfShards);

        public int GetRandomShardIndex()
        {
            return (int)StrongRandomNumberGenerator.Get32Bits(NumberOfShards);
        }


        public void AssignRandomBit(int valueToAssign, int? shardNumber = null)
        {
            int shard = shardNumber ?? GetRandomShardIndex();
            RemoteHost host = ShardToHostMapping.FindMemberResponsible(shard.ToString());
            RestClientHelper.PostBackground(host.Uri, BitsPath + shard + '/' + valueToAssign);
        }

        public async Task<int> StepAsync(string key, int? heightOfLadderInRungs = null, TimeSpan? timeout = null, CancellationToken cancellationToken = new CancellationToken())
        {
            DateTime whenAddedUtc;
            int topOfLadder = heightOfLadderInRungs ?? MaxLadderHeight;

            bool cacheIndicatesTopOfLadder = CacheOfElementsAtTopOfLadder.TryGetValue(key, out whenAddedUtc);
            if (cacheIndicatesTopOfLadder && DateTime.UtcNow - whenAddedUtc < MinimumCacheFreshnessRequired)
            {

                AssignRandomBit(1);
                AssignRandomBit(1);
                AssignRandomBit(0);
                AssignRandomBit(0);

                return topOfLadder;
            }

            int shard = GetShardIndex(key);
            RemoteHost host = ShardToHostMapping.FindMemberResponsible(shard.ToString());

            int heightBeforeStep = await RestClientHelper.PostAsync<int>(host.Uri, ElementsPath + Uri.EscapeUriString(key), 
                timeout: timeout, cancellationToken: cancellationToken, 
                parameters: (!heightOfLadderInRungs.HasValue) ? null : new object[]
                        {
                            new KeyValuePair<string, int>("heightOfLadderInRungs", topOfLadder)
                        } );

            if (heightBeforeStep < topOfLadder && cacheIndicatesTopOfLadder)
            {
                CacheOfElementsAtTopOfLadder.Remove(key);
            }
            else if (heightBeforeStep == topOfLadder)
            {
                CacheOfElementsAtTopOfLadder[key] = DateTime.UtcNow;
            }

            return heightBeforeStep;
        }

        public async Task<int> GetHeightAsync(string element, int? heightOfLadderInRungs = null, TimeSpan? timeout = null, CancellationToken cancellationToken = new CancellationToken())
        {
            DateTime whenAddedUtc;
            int topOfLadder = heightOfLadderInRungs ?? MaxLadderHeight;

            bool cacheIndicatesTopOfLadder = CacheOfElementsAtTopOfLadder.TryGetValue(element, out whenAddedUtc);
            if (cacheIndicatesTopOfLadder && DateTime.UtcNow - whenAddedUtc < MinimumCacheFreshnessRequired)
            {
                return topOfLadder;
            }

            int shard = GetShardIndex(element);
            RemoteHost host = ShardToHostMapping.FindMemberResponsible(shard.ToString());
            int height = await RestClientHelper.GetAsync<int>(host.Uri, ElementsPath + Uri.EscapeUriString(element), cancellationToken: cancellationToken,
                uriParameters: (!heightOfLadderInRungs.HasValue) ? null : new[]
                        {
                            new KeyValuePair<string, string>("heightOfLadderInRungs", heightOfLadderInRungs.Value.ToString())
                        });

            if (height < topOfLadder && cacheIndicatesTopOfLadder)
            {
                CacheOfElementsAtTopOfLadder.Remove(element);
            }
            else if (height == topOfLadder)
            {
                CacheOfElementsAtTopOfLadder[element] = DateTime.UtcNow;
            }

            return height;
        }

    }

}
