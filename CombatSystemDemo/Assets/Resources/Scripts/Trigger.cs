using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public class Trigger : MonoBehaviour
{
    public class Detection
    {
        public GameObject GO;
        public Collider col;
        public CombatSystem.Soldier DetectedSoldier;

        public Detection(GameObject obj, Collider C, CombatSystem.Soldier sol)
        {
            GO = obj;
            col = C;
            DetectedSoldier = sol;
        }
    }

    public List<Detection> DetectedObjects = new List<Detection>();
    public CombatSystem.Soldier SoldierReference;

    private void OnTriggerEnter(Collider other)
    {
        if (DetectedObjects.Where(O => O.col == other).ToList().Count == 0)
        {
            //this breaks if it encounters the terrian or anything that isn't a soldier
            //as long as things are flat that'll never happen but we should fix that
            if (other.gameObject != this.gameObject && other.gameObject != this.gameObject.transform.parent.gameObject && other.gameObject.name != this.gameObject.name)
                DetectedObjects.Add(new Detection(other.gameObject, other, other.transform.Find("Cone").GetComponent<Trigger>().SoldierReference));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (DetectedObjects.Where(O => O.col == other).ToList().Count != 0)
        {
            DetectedObjects.Remove(DetectedObjects.Where(O => O.col == other).ToList()[0]);
        }
    }
}
