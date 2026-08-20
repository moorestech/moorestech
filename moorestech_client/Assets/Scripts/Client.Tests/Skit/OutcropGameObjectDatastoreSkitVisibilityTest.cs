using Client.Game.InGame.Map.Outcrop;
using CommandForgeGenerator.Command;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Skit
{
    public class OutcropGameObjectDatastoreSkitVisibilityTest
    {
        private GameObject _datastoreObject;

        [TearDown]
        public void TearDown()
        {
            if (_datastoreObject != null) Object.DestroyImmediate(_datastoreObject);
        }

        [Test]
        public void SetActiveFalseHidesEveryOutcropUnderTheDatastore()
        {
            _datastoreObject = new GameObject(nameof(OutcropGameObjectDatastore));
            var datastore = _datastoreObject.AddComponent<OutcropGameObjectDatastore>();
            var outcrop = new GameObject("VeinOutcrop_test");
            outcrop.transform.SetParent(_datastoreObject.transform);

            datastore.SetActive(false);
            Assert.IsFalse(outcrop.activeInHierarchy);

            datastore.SetActive(true);
            Assert.IsTrue(outcrop.activeInHierarchy);
        }

        [Test]
        public void DatastoreIsPartOfTheSkitWorldObjectContract()
        {
            _datastoreObject = new GameObject(nameof(OutcropGameObjectDatastore));
            var datastore = _datastoreObject.AddComponent<OutcropGameObjectDatastore>();

            Assert.IsInstanceOf<ISkitWorldObjectControl>(datastore);
        }
    }
}
