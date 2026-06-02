using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
        fileName = "WeaponDatabase",
        menuName = "Config/WeaponDatabase")]
public class WeaponDatabase : ScriptableObject
{
    public List<WeaponConfig> weapons;
}