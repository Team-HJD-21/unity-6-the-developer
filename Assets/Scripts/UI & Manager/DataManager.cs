using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

//  int로 간주하여 Array 접근 시 반드시 explicit Type-casting할 것
public enum AttributeType
{
    PlayerHealth,
    PlayerBullet,
    TurretBullet,
    TurretMissile,
    ControlUnitPower,
    ControlUnitHealth
}

public class DataManager : MonoBehaviour
{
    [Tooltip("기본 빌드용 값 1 / 개발 빌드용 값 4")]
    [Header("About Game")] public static int CurStage = 4;
    [Header("Shop")] public static int Coin = 0;

    public const int LEVEL_MAX = 3;

    private static int PlayerHpLv = 0;
    private static int PlayerBulletLv = 0;
    private static int TurretBulletLv = 0;
    private static int TurretMissileLv = 0;
    private static int ControlUnitPowerLv = 0;
    private static int ControlUnitHpLv = 0;

    private static int[] LevelList = { 0, 0, 0, 0, 0, 0 };
    
    private static int[] MarginList = { 75, 1, 4, 40, 500, 40 };
    
    private static int[] CostList = { 35, 50, 65, 0, 0 };

    //  Initial attributes
    private static int PlayerHp = 400;
    private static int PlayerBullet = 3;
    private static int TurretBullet = 12;
    private static int TurretMissile = 120;
    private static int ControlUnitPower = 250;
    private static int ControlUnitHp = 1500;

    private static int[] AttributeList = { 400, 3, 12, 120, 250, 1500 };

    //  NOTE : Please Serialize
    public static void ApplyLevelingSystem()
    {
        //  Player
        PlayerHp = PlayerHpLv * MarginList[0] + 400;
        AttributeList[0] = PlayerHp;
        PlayerBullet = PlayerBulletLv *MarginList[1] + 3;
        AttributeList[1] = PlayerBullet;

        //  Turret
        TurretBullet = TurretBulletLv * MarginList[2] + 12;
        AttributeList[2] = TurretBullet;
        TurretMissile = TurretMissileLv * MarginList[3] + 110;
        AttributeList[3] = TurretMissile;

        //  Control Unit
        ControlUnitHp = ControlUnitHpLv * MarginList[4] + 1500;
        AttributeList[(int)AttributeType.ControlUnitHealth] = ControlUnitHp;
        ControlUnitPower = ControlUnitPowerLv * MarginList[5] + 250;
        AttributeList[(int)AttributeType.ControlUnitPower] = ControlUnitPower;

        /*
         * Player Hp : 400 475 550 625 (+ 75)
         * Player Bullet : 3 4 5 6 (+ 1)
         * Turret Bullet : 12 16 20 24 (+ 4)
         * Turret Missile : 110 150 190 230 (+ 40)
         * Control Unit Power : 250 290 330 370 (+ 40)
         * Control Unit Hp : 1500 2000 2500 3000 (+ 500)
         */
    }

    public static bool Upgrade(int mode)
    {
        if (Coin < CostList[LevelList[mode]])
        {
            // Debug.Log("돈 없다 돈 가져와라");
            return false;
        }
        
        if (LevelList[mode] > LEVEL_MAX)
        {
            // Debug.LogError("Level Boundary Error");
            return false;
        }

        Coin -= CostList[LevelList[mode]];
        
        LevelList[mode]++;

        ValidateLevelList();
        ApplyLevelingSystem();

        return true;
    }

    //  Overloading
    public static int GetAttributeData(int mode)
    {
        return AttributeList[mode];
    }
    public static int GetAttributeData(AttributeType mode)
    {
        return AttributeList[(int)mode];
    }

    public static int GetLevel(int mode)
    {
        return LevelList[mode];
    }
    public static int GetLevel(AttributeType mode)
    {
        return LevelList[(int)mode];
    }

    public static int GetCost(int mode)
    {
        return CostList[LevelList[mode]];
    }
    public static int GetCost(AttributeType mode)
    {
        return CostList[LevelList[(int)mode]];
    }

    public static int GetMargin(int mode)
    {
        return MarginList[mode];
    }
    public static int GetMargin(AttributeType mode)
    {
        return MarginList[(int)mode];
    }


    public static void ValidateLevelList()
    {
        PlayerHpLv = LevelList[0];
        PlayerBulletLv = LevelList[1];
        TurretBulletLv = LevelList[2];
        TurretMissileLv = LevelList[3];
        ControlUnitPowerLv = LevelList[4];
        ControlUnitHpLv = LevelList[5];
    }
}
