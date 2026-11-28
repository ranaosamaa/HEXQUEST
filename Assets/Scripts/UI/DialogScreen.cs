using System;
using UnityEngine;
using UnityEngine.UI;

namespace HexQuest
{
    /// <summary>Small modal message with one or two buttons (errors, confirmations).</summary>
    public class DialogScreen : UiScreen
    {
        Text title, body;
        UiButton primary, secondary;
        Action primaryAction, secondaryAction;
        RectTransform panel;

        protected override void OnBuild(RectTransform root)
        {
            var dim = UIKit.NewRect("Dim", root).Stretch();
            dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.8f);
            panel = UIKit.Panel(root, "Panel");
            panel.Place(UIKit.MC, UIKit.MC, Vector2.zero, new Vector2(900f, 520f));
            title = UIKit.AddText(panel, "", 52, Palette.Gold, TextAnchor.MiddleCenter, true).Shadow(3f);
            title.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -36f), new Vector2(800f, 80f));
            UIKit.Divider(panel, 520f, UIKit.TC, new Vector2(0f, -120f));
            body = UIKit.AddText(panel, "", 30, Palette.Cream, TextAnchor.UpperCenter, false).Shadow(1.5f);
            body.rectTransform.Place(UIKit.TC, UIKit.TC, new Vector2(0f, -150f), new Vector2(760f, 200f));
            primary = UIKit.Button(panel, "Primary", "", () => { Close(); if (primaryAction != null) primaryAction(); }, true, 32);
            ((RectTransform)primary.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-50f, 44f), new Vector2(340f, 88f));
            secondary = UIKit.Button(panel, "Secondary", "", () => { Close(); if (secondaryAction != null) secondaryAction(); }, false, 32);
            ((RectTransform)secondary.transform).Place(UIKit.BL, UIKit.BL, new Vector2(50f, 44f), new Vector2(340f, 88f));
        }

        public void Present(string heading, string message, string primaryLabel, Action onPrimary,
                            string secondaryLabel = null, Action onSecondary = null)
        {
            title.text = heading;
            body.text = message;
            primary.Label.text = primaryLabel;
            primaryAction = onPrimary;
            secondaryAction = onSecondary;
            secondary.gameObject.SetActive(secondaryLabel != null);
            if (secondaryLabel != null) secondary.Label.text = secondaryLabel;
            if (secondaryLabel == null)
                ((RectTransform)primary.transform).Place(UIKit.BC, UIKit.BC, new Vector2(0f, 44f), new Vector2(340f, 88f));
            else
                ((RectTransform)primary.transform).Place(UIKit.BR, UIKit.BR, new Vector2(-50f, 44f), new Vector2(340f, 88f));
            Open();
        }

        public override bool OnEscape()
        {
            Close();
            if (secondaryAction != null) secondaryAction();
            return true;
        }
    }
}
