using Barotrauma;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using MoreLevelContent.Shared;
using MoreLevelContent.Shared.Data;
using MoreLevelContent.Shared.Generation;
using MoreLevelContent.Shared.Utils;
using System;
using System.Linq;
using System.Xml.Linq;

namespace MoreLevelContent.Items
{
    internal class SonarGuide : Powered
    {
        [Serialize(10.0f, IsPropertySaveable.Yes, description: "How often the guide sends out a ping."), Editable]
        public float PingInterval { get; private set; }

        [Serialize(30000.0f, IsPropertySaveable.Yes, description: "How far away this guide can be detected from, 10000.0f is the default sonar range."), Editable]
        public float Range { get; private set; }

        [Serialize(10000.0f, IsPropertySaveable.Yes, description: "How close a player has to be before the map feature is revealed."), Editable]
        public float RevealRange { get; private set; }

        private float _Interval;
        private bool _Revealed;
        public SonarGuide(Item item, ContentXElement element) : base(item, element)
        {
            IsActive = true;
            _Revealed = false;
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

                _Interval = PingInterval;
                foreach (Item item in Item.ItemList)
                {
                    item.GetComponent<Sonar>()?.AddSonarCircle(Item.WorldPosition, (Sonar.BlipType)5, blipCount: 100, range: Range);
                }
            }
#endif
        }
    }
}
