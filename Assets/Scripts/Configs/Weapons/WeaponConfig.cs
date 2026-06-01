using UnityEngine;

public enum WeaponType
{
    Melee,
    Ranged
}

public enum WeaponTag
{
    None,
    Melee,
    Ranged
}

[CreateAssetMenu(
        fileName = "Weapon",
        menuName = "Config/Weapon")]
public class WeaponConfig : ScriptableObject
{

    [Header("基础信息")]
    public string weaponName;
    public Sprite icon;
    public WeaponType weaponType;

    [Header("基础属性")]
    public float damage;
    public float attackSpeed;
    public float range;
    public float knockback;

    [Header("成长")]
    public float damageGrowth;
    public float attackSpeedGrowth;

    [Header("暴击")]
    public float critChance;
    public float critMultiplier;

    [Header("远程专用")]
    public GameObject projectilePrefab;
    public int projectileCount;

    [Header("标签")]
    public WeaponTag[] tags;
}