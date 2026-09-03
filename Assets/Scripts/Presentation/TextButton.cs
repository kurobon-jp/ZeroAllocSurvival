using TMPro;
using UnityEngine;
using ZeroAllocSurvival.Extensions;

namespace ZeroAllocSurvival.Presentation
{
    public class TextButton : BaseButton
    {
       [SerializeField] private TMP_Text _text;

       private void Start()
       {
           _text.Warmup();
       }

       public void SetText(string text)
       {
           _text.SetText(text);
       }
    }
}