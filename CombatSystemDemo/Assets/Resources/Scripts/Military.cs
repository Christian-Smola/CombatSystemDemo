using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

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
        public List<NewMultiThreadingTest.Animation> AnimationList = new List<NewMultiThreadingTest.Animation>();
        public float Attack;
        public float Armor;
        public float MovementSpeed;
        public float EvasionChance;

        public int NumberOfSoldiers;

        public UnitSubType(string name, GameObject prefab, List<NewMultiThreadingTest.Animation> animations, float Atk, float Arm, float speed, float Evasion)
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
        List<NewMultiThreadingTest.Animation> AnimationList = new List<NewMultiThreadingTest.Animation>()
            {
                new NewMultiThreadingTest.Animation("Idle", NewMultiThreadingTest.AnimationType.Idle, 4f),
                new NewMultiThreadingTest.Animation("Walk", NewMultiThreadingTest.AnimationType.Walk, 1.3f),
                new NewMultiThreadingTest.Animation("Attack Ready", NewMultiThreadingTest.AnimationType.CombatIdle, 2f),
                new NewMultiThreadingTest.Animation("Combat Idle", NewMultiThreadingTest.AnimationType.Flavor, 2f),
                new NewMultiThreadingTest.Animation("Block", NewMultiThreadingTest.AnimationType.Block, 1.833333f),
                new NewMultiThreadingTest.Animation("Attack Thrust", NewMultiThreadingTest.AnimationType.Attack, 1.166667f),
                new NewMultiThreadingTest.Animation("Fall Forward", NewMultiThreadingTest.AnimationType.Death, 4.5f),
                new NewMultiThreadingTest.Animation("Hit Left", NewMultiThreadingTest.AnimationType.Hit, 1.4f),
                new NewMultiThreadingTest.Animation("Hit Right", NewMultiThreadingTest.AnimationType.Hit, 2f)
            };

        UnitSubTypeList.Add(new UnitSubType("Legionary Sword", Resources.Load<GameObject>("Updated Units/Legionary Sword"), AnimationList, 90f, 35f, 3f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Legionary Spear", Resources.Load<GameObject>("Updated Units/Legionary Spear"), AnimationList, 90f, 35f, 3f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Centurion Sword", Resources.Load<GameObject>("Updated Units/Centurion Sword"), AnimationList, 90f, 35f, 3f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Centurion Spear", Resources.Load<GameObject>("Updated Units/Centurion Spear"), AnimationList, 90f, 35f, 3f, 120f));

        AnimationList = new List<NewMultiThreadingTest.Animation>()
            {
                new NewMultiThreadingTest.Animation("Idle Battle Simple", NewMultiThreadingTest.AnimationType.Idle, 5.633334f),
                new NewMultiThreadingTest.Animation("Walk Attack", NewMultiThreadingTest.AnimationType.Walk, 1.2f),
                new NewMultiThreadingTest.Animation("Idle Battle", NewMultiThreadingTest.AnimationType.CombatIdle, 3.333333f),
                new NewMultiThreadingTest.Animation("Block Shield", NewMultiThreadingTest.AnimationType.Block, 1.666667f),
                new NewMultiThreadingTest.Animation("Lean Back", NewMultiThreadingTest.AnimationType.Block, 1.333333f),
                new NewMultiThreadingTest.Animation("Attack Cut", NewMultiThreadingTest.AnimationType.Attack, 1.666667f),
                new NewMultiThreadingTest.Animation("Attack Thrust", NewMultiThreadingTest.AnimationType.Attack, 1.466667f),
                new NewMultiThreadingTest.Animation("Attack Combo", NewMultiThreadingTest.AnimationType.Attack, 2f),
                new NewMultiThreadingTest.Animation("Fall Back", NewMultiThreadingTest.AnimationType.Death, 2f),
                new NewMultiThreadingTest.Animation("Hit Left", NewMultiThreadingTest.AnimationType.Hit, 1.833333f),
                new NewMultiThreadingTest.Animation("Hit Right", NewMultiThreadingTest.AnimationType.Hit, 1.666667f)
            };

        UnitSubTypeList.Add(new UnitSubType("Celt Axe", Resources.Load<GameObject>("Updated Units/Celt Axe"), AnimationList, 80f, 50f, 3.5f, 120f));
        UnitSubTypeList.Add(new UnitSubType("Celt Sword", Resources.Load<GameObject>("Updated Units/Celt Sword"), AnimationList, 80f, 50f, 3.5f, 120f));
    }
}
