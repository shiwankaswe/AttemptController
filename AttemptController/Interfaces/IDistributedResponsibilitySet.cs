using System.Collections.Generic;

namespace AttemptController.Interfaces
{

    public interface IDistributedResponsibilitySet<TMember>
    {
        int Count { get; }

        bool ContainsKey(string key);

        void Add(string uniqueKeyIdentifiyingMember, TMember member);

        void AddRange(IEnumerable<KeyValuePair<string, TMember>> newKeyMemberPairs);

        void Remove(string uniqueKeyIdentifiyingMember);

        void RemoveRange(IEnumerable<string> uniqueKeysIdentifiyingMember);

        TMember FindMemberResponsible(string key);

        List<TMember> FindMembersResponsible(string key, int numberOfUniqueMembersToFind);
    }

}
