using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DragonMMA.Tests
{
    public sealed class DragonUiStatusTests
    {
        [Test]
        public void Toolbar_ExposesCampAndSkillProgressWithoutOpeningAPanel()
        {
            var data = new GameSaveData();
            data.storage.Add(new DragonInstance(DragonKind.Baby));
            data.storage.Add(new DragonInstance(DragonKind.Headbutt));
            data.trainingRewardsClaimed[0] = true; data.trainingRewardsClaimed[3] = true;
            Assert.That(DragonUiStatusLabels.Camp(data), Is.EqualTo("거점 2 ↑"));
            Assert.That(DragonUiStatusLabels.Skills(data), Is.EqualTo("기술 2/5"));
        }

        [Test]
        public void Toolbar_TrainingLabelCoversLockedEmptyRunningAndCompleteStates()
        {
            const long now = 1000;
            var data = new GameSaveData();
            Assert.That(DragonUiStatusLabels.Training(data, now), Is.EqualTo("수련 잠김"));
            data.trainingUnlocked = true;
            Assert.That(DragonUiStatusLabels.Training(data, now), Is.EqualTo("수련 배치"));
            data.training[0].dragon = new DragonInstance(DragonKind.Baby); data.training[0].endUtc = now + 61;
            Assert.That(DragonUiStatusLabels.Training(data, now), Is.EqualTo("수련 2분"));
            data.training[0].endUtc = now;
            Assert.That(DragonUiStatusLabels.Training(data, now), Is.EqualTo("수련 완료"));
        }

        [Test]
        public void Toolbar_MarketLabelPrioritizesClaimableRewards()
        {
            const long now = 1000;
            var data = new GameSaveData();
            Assert.That(DragonUiStatusLabels.Market(data, now), Is.EqualTo("판매소"));
            data.sales[0].dragon = new DragonInstance(DragonKind.Baby); data.sales[0].endUtc = now + 30;
            Assert.That(DragonUiStatusLabels.Market(data, now), Is.EqualTo("판매 1/2"));
            data.sales[1].dragon = new DragonInstance(DragonKind.Logtail); data.sales[1].endUtc = now;
            Assert.That(DragonUiStatusLabels.Market(data, now), Is.EqualTo("판매 수령 1"));
        }

        [TestCase("OverlayRoot")] [TestCase("WebOverlayRoot")]
        public void Toolbar_StatusLabelsFitBothAuthoredLayouts(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/DragonMMA/Prefabs/UI/{prefabName}.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var bindings = root.GetComponent<DragonUiBindings>();
                string[] keys = { "Camp", "Training", "Market", "Skills" };
                string[] widest = { "거점 999 ↑", "수련 99시간", "판매 수령 2", "기술 5/5" };
                for (int i = 0; i < keys.Length; i++)
                {
                    Text label = bindings.Get<Text>($"Hunt/Bottom toolbar/{keys[i]}/Label");
                    label.text = widest[i];
                    var button = label.GetComponentInParent<Button>();
                    Assert.That(button, Is.Not.Null, keys[i]);
                    Assert.That(label.preferredWidth + 12, Is.LessThanOrEqualTo(((RectTransform)button.transform).rect.width), $"{prefabName}/{keys[i]}");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
