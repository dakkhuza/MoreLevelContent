using Barotrauma;
using Barotrauma.Items.Components;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Utils;
using System.Xml.Linq;

namespace MoreLevelContent.Items
{
    internal class SonarJammer : Powered
    {
        private readonly int _Strength;
        private bool _ActiveDisturbance;
        public SonarJammer(Item item, ContentXElement element) : base(item, element)
        {
            IsActive = true;
            _Strength = element.GetAttributeInt("strength", 100);
        }

        public override void Update(float deltaTime, Camera cam)
        {
            UpdateOnActiveEffects(deltaTime);

#if CLIENT

            if (Voltage >= MinVoltage && !_ActiveDisturbance)
            {
                SonarExtensions.Instance.Add(Item, _Strength);
                _ActiveDisturbance = true;
                Log.Debug("Added Disturbance");
            }

            if (Voltage < MinVoltage && _ActiveDisturbance)
            {
                SonarExtensions.Instance.Remove(Item);
                _ActiveDisturbance = false;
                Log.Debug("Removed Disturbance");
            }
#endif
        }
    }
}
