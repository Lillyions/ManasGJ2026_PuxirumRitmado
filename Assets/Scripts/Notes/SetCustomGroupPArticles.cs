using UnityEngine;

public class SetCustomGroupParticles : MonoBehaviour
{
    [SerializeField] private ParticleSystem _mainParticleSystem;
    [SerializeField] private ParticleSystem _secondPartiucleSystem;
    [SerializeField] private ParticleSystem _thirdPartcicleSystem;

    public void SetCustomParticles(Color primaryColor, Color secondaryColor)
    {
        var main = _mainParticleSystem.main;
        var secondary = _secondPartiucleSystem.main;
        var third = _thirdPartcicleSystem.main;

        main.startColor = primaryColor;
        secondary.startColor = secondaryColor;
        third.startColor = secondaryColor;
    }
}
