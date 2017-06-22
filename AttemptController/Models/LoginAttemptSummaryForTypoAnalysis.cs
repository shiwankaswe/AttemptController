using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using AttemptController.DataStructures;
using AttemptController.EncryptionPrimitives;

namespace AttemptController.Models
{
    public struct LoginAttemptSummaryForTypoAnalysis
    {
        public string UsernameOrAccountId { get; set; }

        public DecayingDouble Penalty { get; set; }

        public EncryptedStringField EncryptedIncorrectPassword { get; set; }
    }
}
