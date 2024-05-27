using Barotrauma;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Utils;
using System.Xml.Linq;

namespace MoreLevelContent.Items
{
    internal class SonarGuide : Powered
    {
        private readonly float _PingInterval;
        private float _Interval;
        private readonly float _Range;
        public SonarGuide(Item item, ContentXElement element) : base(item, element)
        {
            IsActive = true;
            _PingInterval = element.GetAttributeFloat("interval", 10f);
            _Range = element.GetAttributeFloat("range", Sonar.DefaultSonarRange);
        }

        public override void Update(float deltaTime, Camera cam)
        {
            UpdateOnActiveEffects(deltaTime);

#if CLIENT

            if (Voltage >= MinVoltage)
            {
                if (_Interval > 0)
                {
                    _Interval -= deltaTime;
                    return;
                }

                _Interval = _PingInterval;
                foreach (Item item in Item.ItemList)
                {
                    item.GetComponent<Sonar>()?.AddSonarCircle(Item.WorldPosition, (Sonar.BlipType)5, blipCount: 100, range: _Range);
                }
            }
#endif
        }
    }
}
