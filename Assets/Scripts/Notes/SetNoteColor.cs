using Dypsloom.RhythmTimeline.Core;
using Dypsloom.RhythmTimeline.Core.Notes;
using UnityEngine;

public class SetNoteColor : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _shinyCircle;

    [SerializeField] private SpriteRenderer[] _outerCircle;
    [SerializeField] private SpriteRenderer[] _innerCircle;
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private ParticleSystem _particles;

    private Note _noteScript;

    private void Awake()
    {
        _noteScript = GetComponent<Note>();
        if (_noteScript != null)
        {
            _noteScript.OnReset += CallColorChange;
            _noteScript.OnInitialize += CallColorChange;
        }
    }

    private void OnEnable()
    {
        _shinyCircle.gameObject.SetActive(false);
    }

    private void CallColorChange(Note note)
    {
        TrackObject currentTrack = note.RhythmClipData.TrackObject;
        SetNoteColorToTrack(currentTrack.PrimaryColor, currentTrack.SecondaryColor, currentTrack.ShinyCircleMat);
    }

    public void SetNoteColorToTrack(Color trackColor1, Color trackColor2, Material shinyMaterial)
    {
        foreach (SpriteRenderer sprite in _outerCircle)
        {
            sprite.color = trackColor1;
        }

        foreach (SpriteRenderer sprite in _innerCircle)
        {
            sprite.color = trackColor2;
        }

        if(_shinyCircle != null) _shinyCircle.material = shinyMaterial;

        if(_lineRenderer != null)
        {
            _lineRenderer.startColor = trackColor1;
            _lineRenderer.endColor = trackColor1;
        }

        if(_particles != null)
        {
            ParticleSystem.MainModule mainMod = _particles.main;
            mainMod.startColor = trackColor2;
        }
    }
}
