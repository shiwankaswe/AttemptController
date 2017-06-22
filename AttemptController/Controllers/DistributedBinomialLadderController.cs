using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using AttemptController.Clients;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;

namespace AttemptController.Controllers
{

    [Route("api/DBLS")]
    public class DistributedBinomialLadderFilterController
    {

        protected DistributedBinomialLadderFilterClient FilterClient;


        protected Dictionary<int,FilterArray> ShardsByIndex;


        protected int NumberOfBitsPerShard;


        protected string SecretSaltToPreventAlgorithmicComplexityAttacks;

        protected int MaxLadderHeight => FilterClient.MaxLadderHeight;

        public DistributedBinomialLadderFilterController(DistributedBinomialLadderFilterClient distributedBinomialLadderFilterClient,
            int numberOfBitsPerShard, string secretSaltToPreventAlgorithmicComplexityAttacks)
        {
            FilterClient = distributedBinomialLadderFilterClient;
            NumberOfBitsPerShard = numberOfBitsPerShard;
            SecretSaltToPreventAlgorithmicComplexityAttacks = secretSaltToPreventAlgorithmicComplexityAttacks;
        }

        protected FilterArray GetShard(int shardNumber)
        {
            if (!ShardsByIndex.ContainsKey(shardNumber))
            {
                ShardsByIndex[shardNumber] = new FilterArray(NumberOfBitsPerShard, MaxLadderHeight, true, SecretSaltToPreventAlgorithmicComplexityAttacks);
            }
            return ShardsByIndex[shardNumber];
        }

        protected FilterArray GetShard(string element)
            => GetShard(FilterClient.GetShardIndex(element));


        [HttpGet("/Elements/{element}")]
        public int GetHeight([FromRoute] string element, [FromQuery] int? heightOfLadderInRungs = null)
        {
            FilterArray shard = GetShard(element);
            return shard.GetIndexesAssociatedWithAnElement(element, heightOfLadderInRungs).Count(
                index => shard[index]);
        }


        [HttpPost("/Elements/{element}")]
        public int DistributedStepAsync([FromRoute]string element, [FromQuery] int? heightOfLadderInRungs)
        {
            FilterArray shard = GetShard(element);
            List<int> rungIndexes = shard.GetIndexesAssociatedWithAnElement(element, heightOfLadderInRungs ?? MaxLadderHeight).ToList();
            List<int> rungsAbove = rungIndexes.Where(rung => !shard[rung]).ToList();

            if (rungsAbove.Count > 0)
            {
                shard.SetBitToOne(
                    rungsAbove[(int) (StrongRandomNumberGenerator.Get32Bits((uint) rungsAbove.Count))]);
            }
            else
            {
                FilterClient.AssignRandomBit(1);
                FilterClient.AssignRandomBit(1);
            }

            FilterClient.AssignRandomBit(0);
            FilterClient.AssignRandomBit(0);

            return rungIndexes.Count - rungsAbove.Count;
        }

        [HttpPost("/Bits/{shardNumber}/{valueToAssign}")]
        public void AssignRandomBit([FromRoute] int shardNumber, [FromRoute] int valueToAssign)
        {
            FilterArray shard = GetShard(shardNumber);
            shard.AssignRandomBit(valueToAssign);
        }

    }

}
