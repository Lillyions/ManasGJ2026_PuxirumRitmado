using DG.Tweening;
using Dypsloom.RhythmTimeline.Core.Notes;
using UnityEngine;


/// <summary>
/// Script to make notes sprites appear to fade in, in the screen
/// </summary>
public class NoteFading : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _sprites;
    [SerializeField] private LineRenderer _line;
    [SerializeField] private float _fadeInTime;
    [SerializeField] private float _fadeDelay;
    private Note _noteScript;

    private void Awake()
    {
        _noteScript = GetComponent<Note>();
        if (_noteScript != null)
        {
            _noteScript.OnReset += CallFadeSprite;
            _noteScript.OnInitialize += CallFadeSprite;
        }
    }

    private void OnDestroy()
    {
        if (_noteScript != null)
        {
            _noteScript.OnReset -= CallFadeSprite;
            _noteScript.OnInitialize -= CallFadeSprite;
        }
    }

    private void CallFadeSprite(Note note)
    {
        foreach (SpriteRenderer sprite in _sprites)
        {
            SetSpriteAlpha(sprite, 0);
            FadeSprite(sprite);
        }
    }

    private void FadeSprite(SpriteRenderer sprite)
    {
        sprite.DOKill();

        sprite.DOFade(1F, _fadeInTime).SetDelay(_fadeDelay).SetEase(Ease.Linear).OnComplete(() => SetSpriteAlpha(sprite, 1));

        if (_line != null)
        {
            _line.DOKill(); // Better practice to use the shortcut if available

            SetLineAlpha(0);

            DOTween.To(
                () => _line.startColor.a,
                x => SetLineAlpha(x), 
                1f,
                _fadeInTime
            )
            .SetDelay(_fadeDelay)
            .SetEase(Ease.Linear)
            .SetTarget(_line);
        }
    }

    private void SetSpriteAlpha(SpriteRenderer sprite, int alphaValue)
    {
        Color col = sprite.color;  
        col.a = alphaValue;
        sprite.color = col;
    }

    private void SetLineAlpha(float alphaValue)
    {
        Color temp = _line.startColor;
        temp.a = alphaValue;
        _line.startColor = temp;
        _line.endColor = temp;
    }
}
