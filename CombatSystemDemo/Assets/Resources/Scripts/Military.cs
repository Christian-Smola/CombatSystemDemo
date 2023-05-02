using System.Collections.Generic;
using UnityEngine;

public class Military
{
    public class Army
    {
        public GameObject ArmyGO;
        public List<Division> DivisionList = new List<Division>();
    }

    public class Division
    {
        public UnitType Type;
        public List<UnitSubType> SubTypeList;

        public Division(UnitType type, List<UnitSubType> subTypes)
        {
            Type = type;
            SubTypeList = subTypes;
        }
    }

    public class UnitSubType
    {
        public string Name;
        public GameObject Prefab;
        public List<CombatSystem.Animation> AnimationList = new List<CombatSystem.Animation>();
        public float Attack;
        public float Armor;
        public float MovementSpeed;
        public float EvasionChance;

        public int NumberOfSoldiers;

        public UnitSubType(string name, GameObject prefab, List<CombatSystem.Animation> animations, float Atk, float Arm, float speed, float Evasion)
        {
            Name = name;
            Prefab = prefab;
            AnimationList = animations;
            Attack = Atk;
            Armor = Arm;
            MovementSpeed = speed;
            EvasionChance = Evasion;
        }

        public UnitSubType()
        {

        }
    }

    public enum UnitType
    {
        LightInfantry, HeavyInfantry, LightCavalry, HeavyCavalry, Skirmishers, Special
    }

    public static List<Army> ArmyList = new List<Army>();
    public static List<UnitSubType> UnitSubTypeList = new List<UnitSubType>();

    public static void PopulateUnitSubTypes()
    {
        List<CombatSystem.Animation> AnimationList = new List<CombatSystem.Animation>()
            {
                new CombatSystem.Animation("Idle", CombatSystem.AnimationType.Idle, 4f),
                new CombatSystem.Animation("Walk", CombatSystem.AnimationType.Walk, 1.3f),
                new CombatSystem.Animation("Attack Ready", CombatSystem.AnimationType.CombatIdle, 2f),
                new CombatSystem.Animation("Combat Idle", CombatSystem.AnimationType.Flavor, 2f),
                new CombatSystem.Animation("Block", CombatSystem.AnimationType.Block, 1.833333f),
                new CombatSystem.Animation("Attack Thrust", CombatSystem.AnimationType.Attack, 1.166667f),
                new CombatSystem.Animation("Fall Forward", CombatSystem.AnimationType.Death, 4.5f),
                new CombatSystem.Animation("Hit Left", CombatSystem.AnimationType.Hit, 1.4f),
                new CombatSystem.Animation("Hit Right", CombatSystem.AnimationType.Hit, 2f)
            };

        UnitSubTypeList.Add(new UnitSubType("Legionary Sword", Resources.Load<GameObject>("Updated Units/Legionary Sword"), AnimationList, 90f, 35f, 3f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Legionary Spear", Resources.Load<GameObject>("Updated Units/Legionary Spear"), AnimationList, 90f, 35f, 3f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Centurion Sword", Resources.Load<GameObject>("Updated Units/Centurion Sword"), AnimationList, 90f, 35f, 3f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Centurion Spear", Resources.Load<GameObject>("Updated Units/Centurion Spear"), AnimationList, 90f, 35f, 3f, 120f));

        AnimationList = new List<CombatSystem.Animation>()
            {
                new CombatSystem.Animation("Idle Battle Simple", CombatSystem.AnimationType.Idle, 5.633334f),
                new CombatSystem.Animation("Walk Attack", CombatSystem.AnimationType.Walk, 1.2f),
                new CombatSystem.Animation("Idle Battle", CombatSystem.AnimationType.CombatIdle, 3.333333f),
                new CombatSystem.Animation("Block Shield", CombatSystem.AnimationType.Block, 1.666667f),
                new CombatSystem.Animation("Lean Back", CombatSystem.AnimationType.Block, 1.333333f),
                new CombatSystem.Animation("Attack Cut", CombatSystem.AnimationType.Attack, 1.666667f),
                new CombatSystem.Animation("Attack Thrust", CombatSystem.AnimationType.Attack, 1.466667f),
                new CombatSystem.Animation("Attack Combo", CombatSystem.AnimationType.Attack, 2f),
                new CombatSystem.Animation("Fall Back", CombatSystem.AnimationType.Death, 2f),
                new CombatSystem.Animation("Hit Left", CombatSystem.AnimationType.Hit, 1.833333f),
                new CombatSystem.Animation("Hit Right", CombatSystem.AnimationType.Hit, 1.666667f)
            };

        UnitSubTypeList.Add(new UnitSubType("Celt Axe", Resources.Load<GameObject>("Updated Units/Celt Axe"), AnimationList, 80f, 50f, 3.5f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Celt Sword", Resources.Load<GameObject>("Updated Units/Celt Sword"), AnimationList, 80f, 50f, 3.5f, 120f));
    }
}
