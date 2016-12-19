using System;
using System.Collections.Generic;
using System.Linq;
using SimonScreams;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Simon Screams
/// Created by Timwi
/// </summary>
public class SimonScreamsModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public KMSelectable[] Buttons;

    void Start()
    {
        Debug.Log("[Simon Screams] Started");
    }

    void ActivateModule()
    {
        Debug.Log("[Simon Screams] Activated");
    }
}
