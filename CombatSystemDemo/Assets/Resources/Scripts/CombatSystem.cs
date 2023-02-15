using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime;
using System.Threading;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using System.Data;
using Unity.VisualScripting;

public class CombatSystem : MonoBehaviour
{
    public class Soldier
    {
        public GameObject SoldierGO;
        public Division AssignedDivision;
        public Military.UnitType SoldierType;
        public Military.UnitSubType SubType;
        public NavMeshAgent NavAgent;
        public Vector3 CurrentPosition;
        public Vector3 SoldierDestination;
        public Vector2 PositionInFormation;
        public Animation AnimState;
        public Trigger trig;
        public Rank rank;
        public State state;
        public bool IsInMotion = false;
        public bool IsAlive = true;
        public float Health = 50f;
        public System.Timers.Timer AnimationTimer = new System.Timers.Timer();

        public Soldier(GameObject MainGO, Military.UnitType type, Military.UnitSubType sub, Animation Anim, Rank Xrank, State st, NavMeshAgent agent)
        {
            SoldierGO = MainGO;
            SoldierType = type;
            SubType = sub;
            AnimState = Anim;
            rank = Xrank;
            state = st;
            NavAgent = agent;
        }
    }

    public class Division
    {
        public int Width;
        public int MaxWidth;
        public int MinWidth;
        public int TempWidth;
        public int LastWidth;
        public Vector3 TempPos;
        public Vector3 Destination;
        public Side side;
        public Formation formation;
        public GameObject DivisionGO;
        public List<ActivateAndDeactivate> ActivationList = new List<ActivateAndDeactivate>();
        public bool IsInMotion = false;
        public bool IsInCombat = false;
        public bool NearbyEnemies = false;
        public bool IsFlipped = false;
        public List<Soldier> SoldierList = new List<Soldier>();

        public Direction CurrentDirection = Direction.Forward;
        public Direction PreviousDirection = Direction.Forward;
        public List<Direction> FlanksUnderAttack = new List<Direction>();
        public List<Direction> FlanksProtected = new List<Direction>();

        public List<Vector3> Boundaries = new List<Vector3>();
        public List<DetectionZone> DetectionZones = new List<DetectionZone>();
        public List<Soldier> CornerSoldiers = new List<Soldier>();
        public List<Soldier> LastCornerSoldiersList = new List<Soldier>();
        public List<Soldier> PreviousRemainder = new List<Soldier>();

        public Division(Side xSide, Formation form, GameObject GO, List<Soldier> soldiers, int width)
        {
            side = xSide;
            formation = form;
            DivisionGO = GO;
            SoldierList = soldiers;
            Width = width;
        }
    }

    public class RaycastSoldier
    {
        public Soldier Soldier;
        public Direction dir;

        public RaycastSoldier(Soldier Sol, Direction direct)
        {
            Soldier = Sol;
            dir = direct;
        }
    }

    public class Animation
    {
        public string AnimationName;
        public AnimationType Type;
        public float Duration;

        public Animation(string Name, AnimationType type, float Dur)
        {
            AnimationName = Name;
            Type = type;
            Duration = Dur;
        }
    }

    public class OpponentDetected
    {
        public Division Opponent;
        public bool Detected;

        public OpponentDetected(Division div, bool HasDivBeenDetected)
        {
            Opponent = div;
            Detected = HasDivBeenDetected;
        }
    }

    public class DetectionZone
    {
        public Direction dir;
        public List<Vector3> Corners = new List<Vector3>();

        public DetectionZone(Direction D, List<Vector3> C)
        {
            dir = D;
            Corners = C;
        }
    }

    public class ActivateAndDeactivate
    {
        public string Activate;
        public string Deactivate;

        public ActivateAndDeactivate(string A, string D)
        {
            Activate = A;
            Deactivate = D;
        }
    }

    public class Line
    {
        public string LineName;
        public Vector2 StartPoint = new Vector2();
        public Vector2 EndPoint = new Vector2();

        public Line(string Name, Vector2 Start, Vector2 End)
        {
            LineName = Name;
            StartPoint = Start;
            EndPoint = End;
        }
    }

    public enum Direction
    {
        Forward, Back, Left, Right, LeftForward, LeftBack, RightForward, RightBack
    }

    public enum Rank
    {
        Centurion, Optio, Legionary, Celt
    }

    public enum State
    {
        Sword, Spear, JS, Axe
    }

    public enum Side
    {
        Side1, Side2, Side3, Side4, Neurtal
    }
    public enum Formation
    {
        Rect, Wedge
    }

    //Worth having a dodge type? it'd be used interchangably with block
    public enum AnimationType
    {
        Idle, CombatIdle, Walk, Attack, Block, Flavor, Hit, Death
    }

    bool SwitchBoundaryDisplay = true;
    bool UnfreezeGame = true;

    private bool DeathToChildThread = false;
    private Thread ChildThread;
    public List<Action> FunctionsToRunInChildThread = new List<Action>();
    public List<Action> FunctionsToRunInMainThread = new List<Action>();

    public List<Division> SelectedDivisions = new List<Division>();
    public List<Division> DivisionList = new List<Division>();
    public List<Material> MaterialList = new List<Material>();
    //public Animation[] AnimationArray = new Animation[6] { new Animation("Idle", 4f), new Animation("Walk", 1.3f), new Animation("Attack Ready", 2f), new Animation("Combat Idle", 2f), new Animation("Block", 1.833333f), new Animation("Attack Thrust", 1.166667f) };

    private Vector2 SelectionBoxStartPos;
    private Vector3 DivisionPlacementBoxStartPos;
    private Vector3 DivisionPlacementBoxCurrentPos;
    public GameObject SelectionBox;
    private List<GameObject> ListofDivisionPlacementUIElements = new List<GameObject>();

    public System.Diagnostics.Stopwatch DivisionPlacementTimer;
    public System.Diagnostics.Stopwatch UpdateLogicTimer;
    private Ray ray;

    // Start is called before the first frame update
    void Start()
    {
        //Nations.PopulateNationsListLight();
        Military.PopulateUnitSubTypes();

        int DivisionWidth = 15;

        int NumberOfDivisions = 7;

        float Offset = 0f;

        if (NumberOfDivisions % 2 == 0)
            Offset = 0.5f;

        for (int x = 0; x < NumberOfDivisions; x++)
        {
            GameObject GO = new GameObject();
            GO.transform.position = new Vector3((x * 18f) + 500f - (18f * ((NumberOfDivisions / 2) - Offset)), 0f, 475f);

            GO.name = "Celtic Division " + x.ToString();

            Division div = new Division(Side.Side1, Formation.Rect, GO, CreateNewSoldierList(128, DivisionWidth, GO, State.Axe, Military.UnitSubTypeList.Where(S => S.Name == "Celt Axe").ToList()[0], Military.UnitType.LightInfantry), DivisionWidth);

            div.IsInMotion = true;

            CalculateMaxAndMinWidth(div);

            //Not sure I agree that this should be done in the child thread considering it's an OnStart Function
            lock (FunctionsToRunInChildThread)
                FunctionsToRunInChildThread.Add(() => AssignSoldiersToTheirDiv(div));

            GO.transform.eulerAngles = new Vector3(0f, 0f, 0f);

            EstablishDivisionBorders(div);
            DivisionList.Add(div);
        }

        DivisionWidth = 12;

        NumberOfDivisions = 5;

        if (NumberOfDivisions % 2 == 0)
            Offset = 0.5f;

        //for (int x = 0; x < NumberOfDivisions; x++)
        //{
        //    GameObject GO = new GameObject();
        //    GO.transform.position = new Vector3((x * 15f) + 500f - (15f * ((NumberOfDivisions / 2) - Offset)), 0f, 525f);

        //    GO.name = "Roman Division " + x.ToString();

        //    Division div = new Division(Side.Side2, Formation.Rect, GO, CreateNewSoldierList(68, DivisionWidth, GO, State.Sword, Military.UnitSubTypeList.Where(S => S.Name == "Legionary Sword").ToList()[0], Military.UnitType.HeavyInfantry), DivisionWidth);

        //    div.IsInMotion = true;

        //    CalculateMaxAndMinWidth(div);
        //    FunctionsToRunInChildThread.Add(() => AssignSoldiersToTheirDiv(div));

        //    GO.transform.eulerAngles = new Vector3(0f, 180f, 0f);

        //    EstablishDivisionBorders(div);
        //    DivisionList.Add(div);
        //}

        ChildThread = new Thread(ChildThreadFunction);
        ChildThread.Start();

        SelectionBox.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        DisplayDivisionBoundaries3();

        if (Input.GetKeyDown(KeyCode.G))
            SwitchBoundaryDisplay = !SwitchBoundaryDisplay;

        if (Input.GetKeyDown(KeyCode.Space))
            UnfreezeGame = !UnfreezeGame;

        if (Input.GetMouseButtonDown(0))
        {
            if (ListofDivisionPlacementUIElements.Count > 0)
            {
                foreach (GameObject element in ListofDivisionPlacementUIElements)
                    Destroy(element);

                ListofDivisionPlacementUIElements.Clear();
            }

            SelectionBoxStartPos = Input.mousePosition;
            SelectionBox.SetActive(true);

            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        }

        if (Input.GetMouseButton(0))
            UpdateBoxSelected();

        if (Input.GetMouseButtonUp(0))
        {
            //if the selectionbox is too small or not enough time has passed use the original raycast method
            if (Math.Abs(SelectionBoxStartPos.x - Input.mousePosition.x) < 30 && Math.Abs(SelectionBoxStartPos.y - Input.mousePosition.y) < 30)
            {
                foreach (Division div in SelectedDivisions)
                    ToggleDivisionSelection(div, false);

                SelectedDivisions.Clear();

                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 400))
                {
                    if (hit.transform.tag == "Division")
                    {
                        foreach (Division div in DivisionList)
                        {
                            if (hit.transform.parent.gameObject == div.DivisionGO)
                            {
                                SelectedDivisions.Add(div);
                                ToggleDivisionSelection(div, true);
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                SelectionBox.SetActive(false);

                foreach (Division div in SelectedDivisions)
                    ToggleDivisionSelection(div, false);

                SelectedDivisions.Clear();

                RectTransform rectTrans = SelectionBox.GetComponent<RectTransform>();

                Vector2 Min = rectTrans.anchoredPosition - (rectTrans.sizeDelta / 2);
                Vector2 Max = rectTrans.anchoredPosition + (rectTrans.sizeDelta / 2);
                Vector2 LowerRightCorner = new Vector2(Max.x, Min.y);
                Vector2 UpperLeftCorner = new Vector2(Min.x, Max.y);

                foreach (Division div in DivisionList)
                {
                    Vector3 DivMaxCorner = Camera.main.WorldToScreenPoint(div.Boundaries[0]);
                    Vector3 DivMinCorner = Camera.main.WorldToScreenPoint(div.Boundaries[3]);

                    //UI element is inside the division's boundaries
                    if (DivMinCorner.x < Min.x && DivMinCorner.y < Min.y && DivMaxCorner.x > Max.x && DivMaxCorner.y > Max.y)
                    {
                        Debug.Log("UI element is inside the division's boundries");
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        break;
                    }

                    for (int x = 0; x < /*div.BoundaryOffsets.Count*/ div.Boundaries.Count; x++)
                    {
                        Vector3 ScreenPos = Camera.main.WorldToScreenPoint(div.Boundaries[x]);

                        //Corner is inside UI element
                        if (ScreenPos.x > Min.x && ScreenPos.y > Min.y && ScreenPos.x < Max.x && ScreenPos.y < Max.y)
                        {
                            //Debug.Log("Corner is inside the UI element");
                            SelectedDivisions.Add(div);
                            ToggleDivisionSelection(div, true);
                            break;
                        }
                    }

                    if (SelectedDivisions.Contains(div))
                        continue;

                    Vector3 DivUpperLeft = Camera.main.WorldToScreenPoint(div.Boundaries[1]);
                    Vector3 DivLowerRight = Camera.main.WorldToScreenPoint(div.Boundaries[2]);

                    //Start with lines that should normally be perpendicular 

                    //Does Line AB intersect Line EG
                    if (LinesIntersect(DivUpperLeft, DivMaxCorner, UpperLeftCorner, Min))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AB intersects Line EG");
                        continue;
                    }
                    //Does Line AB intersect Line FH
                    if (LinesIntersect(DivUpperLeft, DivMaxCorner, Max, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AB intersects Line FH");
                        continue;
                    }
                    //Does Line CD intersect Line EG
                    if (LinesIntersect(DivMinCorner, DivLowerRight, UpperLeftCorner, Min))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line CD intersects Line EG");
                        continue;
                    }
                    //Does Line CD intersect Line FH
                    if (LinesIntersect(DivMinCorner, DivLowerRight, Max, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line CD intersects Line FH");
                        continue;
                    }
                    //Does Line AC intersect Line EF
                    if (LinesIntersect(DivUpperLeft, DivMinCorner, UpperLeftCorner, Max))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AC intersect Line EF");
                        continue;
                    }
                    //Does Line AC intersect Line GH
                    if (LinesIntersect(DivUpperLeft, DivMinCorner, Min, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AC intersect Line GH");
                        continue;
                    }
                    //Does Line BD intersect Line EF
                    if (LinesIntersect(DivMaxCorner, DivLowerRight, UpperLeftCorner, Max))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line BD intersect Line EF");
                        continue;
                    }
                    //Does Line BD intersect Line GH
                    if (LinesIntersect(DivMaxCorner, DivLowerRight, Min, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line BD intersect Line GH");
                        continue;
                    }
                    //Does Line AB intersect Line EF
                    if (LinesIntersect(DivUpperLeft, DivMaxCorner, UpperLeftCorner, Max))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AB intersects Line EF");
                        continue;
                    }
                    //Does Line AB intersect Line GH
                    if (LinesIntersect(DivUpperLeft, DivMaxCorner, Min, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AB intersects Line GH");
                        continue;
                    }
                    //Does Line CD intersect Line EF
                    if (LinesIntersect(DivMinCorner, DivLowerRight, UpperLeftCorner, Max))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line CD intersects Line EF");
                        continue;
                    }
                    //Does Line CD intersect Line GH
                    if (LinesIntersect(DivMinCorner, DivLowerRight, Min, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line CD intersects Line GH");
                        continue;
                    }
                    //Does Line AC intersect Line EG
                    if (LinesIntersect(DivUpperLeft, DivMinCorner, UpperLeftCorner, Min))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AC intersects Line EG");
                        continue;
                    }
                    //Does Line AC intersect Line FH
                    if (LinesIntersect(DivUpperLeft, DivMinCorner, Max, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line AC intersects Line FH");
                        continue;
                    }
                    //Does Line BD intersect Line EG
                    if (LinesIntersect(DivMaxCorner, DivLowerRight, UpperLeftCorner, Min))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line BD intersects Line EG");
                        continue;
                    }
                    //Does Line BD intersect Line FH
                    if (LinesIntersect(DivMaxCorner, DivLowerRight, Max, LowerRightCorner))
                    {
                        SelectedDivisions.Add(div);
                        ToggleDivisionSelection(div, true);
                        Debug.Log("Line BD intersects Line FH");
                        continue;
                    }
                }
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            DivisionPlacementTimer = System.Diagnostics.Stopwatch.StartNew();

            DivisionPlacementBoxStartPos = Input.mousePosition;

            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        }

        if (Input.GetMouseButton(1))
        {
            if (Input.mousePosition != DivisionPlacementBoxCurrentPos)
            {
                DivisionPlacementBoxCurrentPos = Input.mousePosition;

                //This needs to be optimized, rn it's having a major effect on our frames
                if (Vector3.Distance(DivisionPlacementBoxStartPos, DivisionPlacementBoxCurrentPos) >= 100)
                    UpdateDivisionPlacement();
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            if (SelectedDivisions.Count < 1)
                return;

            if (DivisionPlacementTimer.ElapsedMilliseconds >= 15 && Vector3.Distance(DivisionPlacementBoxStartPos, DivisionPlacementBoxCurrentPos) >= 100)
            {
                float Angle = NewDivisionAngle() + 90f;

                //For Testing Purposes
                foreach (Division div in SelectedDivisions)
                {
                    float AngleChange = div.DivisionGO.transform.localEulerAngles.y - Angle;

                    while (AngleChange > 360 || AngleChange < 0)
                    {
                        if (AngleChange > 360)
                            AngleChange -= 360;
                        else if (AngleChange < 0)
                            AngleChange += 360;
                    }

                    switch (AngleChange)
                    {
                        case float a when a <= 45f || a >= 315f:
                            UnitTest_DirectionStandardization(Direction.Forward, Angle);
                            break;
                        case float a when a >= 135f && a <= 225f:
                            UnitTest_DirectionStandardization(Direction.Back, Angle);
                            break;
                        case float a when a > 225f && a < 315f:
                            UnitTest_DirectionStandardization(Direction.Right, Angle);
                            break;
                        case float a when a < 135 && a > 45f:
                            UnitTest_DirectionStandardization(Direction.Left, Angle);
                            break;
                    }
                }

                foreach (Division div in SelectedDivisions)
                {
                    float AngleChange = div.DivisionGO.transform.localEulerAngles.y - Angle;

                    GameObject go = new GameObject();
                    go.transform.position = div.TempPos;
                    go.transform.localEulerAngles = new Vector3(0f, Angle /*+ 90f*/, 0f);

                    foreach (Soldier child in div.SoldierList)
                        child.SoldierGO.transform.SetParent(go.transform);

                    string strName = div.DivisionGO.name;

                    Destroy(div.DivisionGO);
                    go.name = strName;
                    div.DivisionGO = go;

                    while (AngleChange > 360 || AngleChange < 0)
                    {
                        if (AngleChange > 360)
                            AngleChange -= 360;
                        else if (AngleChange < 0)
                            AngleChange += 360;
                    }

                    div.PreviousDirection = div.CurrentDirection;

                    switch (AngleChange)
                    {
                        case float a when a <= 45f || a >= 315f:
                            UpdateSoldierPositionsInFormationNew(div, Direction.Forward);
                            break;

                        case float a when a >= 135f && a <= 225f:
                            UpdateSoldierPositionsInFormationNew(div, Direction.Back);
                            break;

                        case float a when a > 225f && a < 315f:
                            UpdateSoldierPositionsInFormationNew(div, Direction.Right);
                            break;

                        case float a when a < 135 && a > 45f:
                            UpdateSoldierPositionsInFormationNew(div, Direction.Left);
                            break;

                    }

                    div.LastWidth = div.Width;
                    div.Width = div.TempWidth;
                    div.IsInMotion = true;

                    //It's possible this should be done in AssignSoldierDestinations 
                    //Just a thought :P
                    for (int x = 0; x < div.SoldierList.Count; x++)
                        div.SoldierList[x].IsInMotion = true;
                }
            }
            else
            {
                RaycastHit hit;
                ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out hit, 500))
                {
                    if (hit.transform.tag == "Terrain")
                    {

                        RapidDeployment(hit.point);
                        //float TotalWidth = 0f;
                        //float AverageAngle = 0f;
                        //Direction dir = Direction.Forward;

                        //foreach (Division div in SelectedDivisions)
                        //{
                        //    TotalWidth += div.Width + 0.25f;

                        //    float Angle = (Mathf.Atan2(hit.point.x - div.DivisionGO.transform.position.x, hit.point.z - div.DivisionGO.transform.position.z) * Mathf.Rad2Deg) - div.DivisionGO.transform.localEulerAngles.y;

                        //    while (Angle > 360 || Angle < 0)
                        //    {
                        //        if (Angle > 360)
                        //            Angle -= 360;
                        //        else if (Angle < 0)
                        //            Angle += 360;
                        //    } 

                        //    switch (Angle)
                        //    {
                        //        case float a when a <= 45f || a >= 315f:
                        //            Angle = 0;
                        //            break;
                        //        case float a when a > 45f && a < 135f:
                        //            Angle = 90;
                        //            break;
                        //        case float a when a >= 135f && a <= 225f:
                        //            Angle = 180;
                        //            break;
                        //        case float a when a > 225f && a < 315f:
                        //            Angle = 270;
                        //            break;
                        //    }

                        //    AverageAngle += Angle;
                        //}

                        //AverageAngle = AverageAngle / SelectedDivisions.Count;

                        //while (AverageAngle > 360 || AverageAngle < 0)
                        //{
                        //    if (AverageAngle > 360)
                        //        AverageAngle -= 360;
                        //    else if (AverageAngle < 0)
                        //        AverageAngle += 360;
                        //}

                        //switch (AverageAngle)
                        //{
                        //    case float a when a <= 45f || a >= 315f:
                        //        dir = Direction.Forward;
                        //        break;
                        //    case float a when a > 45f && a < 135f:
                        //        dir = Direction.Right;
                        //        break;
                        //    case float a when a >= 135f && a <= 225f:
                        //        dir = Direction.Back;
                        //        break;
                        //    case float a when a > 225f && a < 315f:
                        //        dir = Direction.Left;
                        //        break;
                        //}

                        //Vector3 A = new Vector3(hit.point.x + (-(TotalWidth / 2) * Mathf.Cos(AverageAngle * (Mathf.PI / 180f))), 0f, hit.point.z + (-(TotalWidth / 2) * Mathf.Sin(AverageAngle * Mathf.PI / 180f)));
                        //Vector3 B = new Vector3(hit.point.x - (-(TotalWidth / 2) * Mathf.Cos(AverageAngle * (Mathf.PI / 180f))), 0f, hit.point.z - (-(TotalWidth / 2) * Mathf.Sin(AverageAngle * Mathf.PI / 180f)));

                        //Vector3 AB = A - B;

                        //Debug.DrawLine(hit.point, hit.point + (Vector3.up * 6f), Color.white, 50);
                        //Debug.DrawLine(A, A + (Vector3.up * 6f), Color.blue, 50);
                        //Debug.DrawLine(B, B + (Vector3.up * 6f), Color.red, 50);

                        //Dictionary<float, Division> DivDistPairs = new Dictionary<float, Division>();
                        //List<Vector3> Destinations = new List<Vector3>();

                        //Vector3 StartPos = new Vector3(0f, 0.1f, 0f) + A + new Vector3((TotalWidth / (SelectedDivisions.Count * 2)) * Mathf.Cos(AverageAngle * (Mathf.PI / 180f)), 0f, (TotalWidth / (SelectedDivisions.Count * 2)) * Mathf.Sin(AverageAngle * (Mathf.PI / 180f)));

                        //for (int x = 0; x < SelectedDivisions.Count; x++)
                        //    Destinations.Add(StartPos - (AB * ((float)x / (float)SelectedDivisions.Count)));

                        //foreach (Vector3 Dest in Destinations)
                        //    Debug.DrawLine(Dest, Dest + (Vector3.up * 4f), Color.magenta, 50);

                        //foreach (Division div in SelectedDivisions)
                        //{
                        //    float CombinedDist = 0f;

                        //    for (int i = 0; i < SelectedDivisions.Count; i++)
                        //        CombinedDist += Math.Abs(Vector3.Distance(div.DivisionGO.transform.position, Destinations[i]));

                        //    DivDistPairs.Add(CombinedDist, div);
                        //}

                        //foreach (KeyValuePair<float, Division> Pair in DivDistPairs.OrderByDescending(D => D.Key))
                        //{
                        //    float ClosestDist = 1000000000f;
                        //    Vector3 ChosenDestination = new Vector3();

                        //    foreach (Vector3 Dest in Destinations)
                        //    {
                        //        float DistToDestination = Math.Abs(Vector3.Distance(Pair.Value.DivisionGO.transform.position, Dest));

                        //        if (DistToDestination < ClosestDist)
                        //        {
                        //            ClosestDist = DistToDestination;
                        //            ChosenDestination = Dest;
                        //        }
                        //    }

                        //    Destinations.Remove(ChosenDestination);

                        //    GameObject go = new GameObject();
                        //    go.transform.position = ChosenDestination;
                        //    go.transform.localEulerAngles = new Vector3(0f, AverageAngle + Pair.Value.DivisionGO.transform.localEulerAngles.y, 0f);

                        //    foreach (Soldier child in Pair.Value.SoldierList)
                        //        child.SoldierGO.transform.SetParent(go.transform);

                        //    string strName = Pair.Value.DivisionGO.name;

                        //    Destroy(Pair.Value.DivisionGO);
                        //    go.name = strName;
                        //    Pair.Value.DivisionGO = go;

                        //    Pair.Value.TempWidth = Pair.Value.Width;

                        //    UpdateSoldierPositionsInFormationNew(Pair.Value, dir);

                        //    Pair.Value.IsInMotion = true;

                        //    foreach (Soldier sol in Pair.Value.SoldierList)
                        //        sol.IsInMotion = true;
                        //}
                    }
                }
            }

            foreach (GameObject go in ListofDivisionPlacementUIElements)
                Destroy(go);

            ListofDivisionPlacementUIElements.Clear();
        }

        QueuedFunctions();
    }

    public void QueuedFunctions()
    {
        List<Action> ActionList = new List<Action>();

        lock (FunctionsToRunInMainThread)
        {
            foreach (Action act in FunctionsToRunInMainThread)
                ActionList.Add(act);

            FunctionsToRunInMainThread.Clear();
        }

        foreach (Action act in ActionList)
            act();
    }

    public void ChildThreadFunction()
    {
        UpdateLogicTimer = System.Diagnostics.Stopwatch.StartNew();

        while (true)
        {
            if (DeathToChildThread)
                ChildThread.Abort();

            List<Action> ActionList = new List<Action>();

            //This function and the one below should not be done in the same frame
            //We definitely want to stagger them
            //if (UpdateLogicTimer.ElapsedMilliseconds > 50f)
            //    ActionList.Add(() => DistanceCheck());

            //100f should = .1 second
            if (UpdateLogicTimer.ElapsedMilliseconds > 100f)
            {
                ActionList.Add(() => UpdateLogic());
                UpdateLogicTimer.Restart();
            }

            lock (FunctionsToRunInChildThread)
            {
                if (FunctionsToRunInChildThread.Count > 0)
                    foreach (Action act in FunctionsToRunInChildThread)
                        ActionList.Add(act);

                FunctionsToRunInChildThread.Clear();
            }

            if (ActionList.Count > 0)
                foreach (Action act in ActionList)
                    act();
            else
            {
                //sleep for a tenth of a second
                Thread.Sleep(100);
            }
        }
    }

    public List<Soldier> CreateNewSoldierList(int NumberOfSoldiers, int width, GameObject GO, State EquipState, Military.UnitSubType sub, Military.UnitType Type = Military.UnitType.HeavyInfantry)
    {
        try
        {
            List<Soldier> soldiers = new List<Soldier>();

            switch (Type)
            {
                case Military.UnitType.HeavyInfantry:
                    int TotalNumberOfColumns = NumberOfSoldiers / width;
                    int CurrentRowIsEven = 0;

                    //if Total # of Soldiers is not divisible by the width, add 1
                    if (NumberOfSoldiers % width != 0)
                        TotalNumberOfColumns++;

                    if (width % 2 == 0)
                        CurrentRowIsEven = 1;

                    for (int RowIndex = 0; RowIndex < TotalNumberOfColumns; RowIndex++)
                    {
                        int CurrentRowWidth = 0;

                        if (soldiers.Count + width < NumberOfSoldiers)
                            CurrentRowWidth = width;
                        else
                            CurrentRowWidth = NumberOfSoldiers - soldiers.Count;

                        int WidthOffset = 0;
                        float xPositionOffset = 0f;

                        if (CurrentRowWidth % 2 != 0)
                            WidthOffset = 1;
                        else
                            xPositionOffset = -0.5f;

                        for (int x = -(CurrentRowWidth / 2) + CurrentRowIsEven; x < (CurrentRowWidth / 2) + WidthOffset + CurrentRowIsEven; x++)
                        {
                            Rank rank = Rank.Legionary;

                            //This offset bit is convoluted, basically it'll be -1 when width is even and zero when width is odd
                            if (RowIndex == 0 && x == (width / 2) /*- 1 + WidthOffset*/)
                                rank = Rank.Centurion;

                            GameObject[] Prefabs = Resources.LoadAll<GameObject>("Updated Units/");

                            GameObject Prefab = (GameObject)Resources.Load("Updated Units/" + rank.ToString() + " " + EquipState.ToString());

                            GameObject UnitGO = Instantiate(Prefab, new Vector3(GO.transform.position.x + x + xPositionOffset, 0f, GO.transform.position.z - (RowIndex * 1f)), GO.transform.rotation, GO.transform);

                            UnitGO.transform.Find(rank.ToString() + " " + EquipState + " Idle").gameObject.SetActive(true);

                            UnitGO.transform.tag = "Division";

                            soldiers.Add(new Soldier(UnitGO, Type, sub, sub.AnimationList.Where(A => A.Type == AnimationType.Idle).ToList()[0], rank, EquipState, UnitGO.GetComponent<NavMeshAgent>()));

                            soldiers[soldiers.Count - 1].PositionInFormation = new Vector2(x, RowIndex);
                        }
                    }
                    break;

                case Military.UnitType.LightInfantry:
                    int TotalNumberOfColumns1 = NumberOfSoldiers / width;
                    int CurrentRowIsEven1 = 0;

                    //if Total # of Soldiers is not divisible by the width, add 1
                    if (NumberOfSoldiers % width != 0)
                        TotalNumberOfColumns1++;

                    if (width % 2 == 0)
                        CurrentRowIsEven = 1;

                    for (int RowIndex = 0; RowIndex < TotalNumberOfColumns1; RowIndex++)
                    {
                        int CurrentRowWidth = 0;

                        if (soldiers.Count + width < NumberOfSoldiers)
                            CurrentRowWidth = width;
                        else
                            CurrentRowWidth = NumberOfSoldiers - soldiers.Count;

                        int WidthOffset = 0;
                        float xPositionOffset = 0f;

                        if (CurrentRowWidth % 2 != 0)
                            WidthOffset = 1;
                        else
                            xPositionOffset = 0.5f;

                        System.Random R = new System.Random();

                        for (int x = -(CurrentRowWidth / 2) + CurrentRowIsEven1; x < (CurrentRowWidth / 2) + WidthOffset + CurrentRowIsEven1; x++)
                        {
                            Rank rank = Rank.Celt;

                            GameObject[] Prefabs = Resources.LoadAll<GameObject>("Updated Units/").Where(G => G.name.Contains(rank.ToString())).ToArray();

                            Texture[] Textures = Resources.LoadAll<Texture>("Imported Folders/Celt Export Folder/Textures").Where(T => T.name.Contains("albedo")).ToArray();

                            GameObject Prefab = Prefabs[R.Next(Prefabs.Count())];

                            GameObject UnitGO = Instantiate(Prefab, new Vector3(GO.transform.position.x + x + xPositionOffset, 0f, GO.transform.position.z - (RowIndex * 1f)), GO.transform.rotation, GO.transform);

                            int TextureIndex = R.Next(Textures.Count());

                            foreach (Transform Child in UnitGO.transform)
                            {
                                if (Child.name == "Cone")
                                    continue;

                                MeshRenderer renderer = Child.GetComponent<MeshRenderer>();
                                renderer.material.SetTexture(renderer.material.shader.GetPropertyName(0), Textures[TextureIndex]);
                            }

                            Animation anim = sub.AnimationList.Where(A => A.Type == AnimationType.Idle).ToList()[0];

                            UnitGO.transform.Find(Prefab.name + " " + anim.AnimationName).gameObject.SetActive(true);

                            UnitGO.transform.tag = "Division";

                            if (Prefab.name.ToString().Contains("Axe"))
                                EquipState = State.Axe;
                            else
                                EquipState = State.Sword;

                            soldiers.Add(new Soldier(UnitGO, Type, sub, anim, rank, EquipState, UnitGO.GetComponent<NavMeshAgent>()));

                            soldiers[soldiers.Count - 1].PositionInFormation = new Vector2(x, RowIndex);
                        }
                    }
                    break;
            }

            foreach (Soldier sol in soldiers)
            {
                sol.trig = sol.SoldierGO.transform.Find("Cone").GetComponent<Trigger>();
                sol.trig.SoldierReference = sol;
            }

            foreach (Soldier sol in soldiers)
                sol.CurrentPosition = sol.SoldierGO.transform.position;

            return soldiers;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
            return null;
        }
    }

    public void UpdateBoxSelected()
    {
        float width = Input.mousePosition.x - SelectionBoxStartPos.x;
        float height = Input.mousePosition.y - SelectionBoxStartPos.y;

        SelectionBox.GetComponent<RectTransform>().sizeDelta = new Vector2(Math.Abs(width), Math.Abs(height));
        SelectionBox.GetComponent<RectTransform>().anchoredPosition = new Vector2(SelectionBoxStartPos.x + (width / 2), SelectionBoxStartPos.y + (height / 2));
    }

    public void UpdateDivisionPlacement()
    {
        Ray CursorRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        Ray StartPosRay = Camera.main.ScreenPointToRay(DivisionPlacementBoxStartPos);

        RaycastHit CurrentPosHit;
        RaycastHit StartPosHit;

        //They use binary instead of int for the layermask
        // << is shifting a bit one spot to the left
        Physics.Raycast(CursorRay, out CurrentPosHit, 1200, 1 << 1);
        Physics.Raycast(StartPosRay, out StartPosHit, 1200, 1 << 1);

        float DistFromStartToCursor = Math.Abs(Vector3.Distance(StartPosHit.point, CurrentPosHit.point));

        float MaxDistance = 0f;
        float MinDistance = 0f;

        //This shouldn't be done here, should be done on selection
        foreach (Division div in SelectedDivisions)
        {
            MaxDistance = MaxDistance + div.MaxWidth + 0.25f;
            MinDistance = MinDistance + div.MinWidth + 0.25f;
        }

        foreach (GameObject go in ListofDivisionPlacementUIElements)
            Destroy(go);

        if (CurrentPosHit.transform.tag == "Terrain" && StartPosHit.transform.tag == "Terrain")
        {
            float Angle = (Mathf.Atan2(StartPosHit.point.x - CurrentPosHit.point.x, StartPosHit.point.z - CurrentPosHit.point.z) * Mathf.Rad2Deg);

            Vector3 AB = CurrentPosHit.point - StartPosHit.point;

            Material mat = Resources.Load<Material>("UI Elements/Orange Material");

            int TempWidth = 0;
            float TempDepth = 0f;

            Vector3 Offset = new Vector3((DistFromStartToCursor / (SelectedDivisions.Count * 2)) * Mathf.Sin(Angle * (Mathf.PI/ 180f)), 0f, (DistFromStartToCursor / (SelectedDivisions.Count * 2)) * Mathf.Cos(Angle * (Mathf.PI / 180f)));

            List<GameObject> UIElementsToAssign = new List<GameObject>();

            for (int x = 0; x < SelectedDivisions.Count; x++)
            {
                GameObject UIelement = GameObject.CreatePrimitive(PrimitiveType.Plane);

                if (DistFromStartToCursor <= MaxDistance && DistFromStartToCursor >= MinDistance)
                {
                    UIelement.transform.position = new Vector3(0f, 0.1f, 0f) - Offset + StartPosHit.point + (AB * ((float)x / (float)SelectedDivisions.Count));
                }
                else if (DistFromStartToCursor > MaxDistance)
                {
                    Vector3 C = new Vector3(StartPosHit.point.x + (-MaxDistance * Mathf.Sin(Angle * (Mathf.PI / 180f))), 0f, StartPosHit.point.z + (-MaxDistance * Mathf.Cos(Angle * (Mathf.PI / 180f))));

                    Vector3 AC = C - StartPosHit.point;

                    UIelement.transform.position = new Vector3(0f, 0.1f, 0f) - Offset + StartPosHit.point + (AC * ((float)x / (float)SelectedDivisions.Count));
                }
                else if (DistFromStartToCursor < MinDistance)
                {
                    Vector3 C = new Vector3(StartPosHit.point.x + (-MinDistance * Mathf.Sin(Angle * (Mathf.PI / 180f))), 0f, StartPosHit.point.z + (-MinDistance * Mathf.Cos(Angle * (Mathf.PI / 180f))));

                    Vector3 AC = C - StartPosHit.point;

                    UIelement.transform.position = new Vector3(0f, 0.1f, 0f) - Offset + StartPosHit.point + (AC * ((float)x / (float)SelectedDivisions.Count));
                }

                UIElementsToAssign.Add(UIelement);
            }

            Dictionary<float, Division> DivDistPairs = new Dictionary<float, Division>();

            foreach (Division div in SelectedDivisions)
            {
                float CombinedDist = 0f;

                foreach (GameObject element in UIElementsToAssign)
                    CombinedDist += Math.Abs(Vector3.Distance(div.DivisionGO.transform.position, element.transform.position));

                DivDistPairs.Add(CombinedDist, div);
            }

            foreach (KeyValuePair<float, Division> Pair in DivDistPairs.OrderByDescending(D => D.Key))
            {
                float ClosestDist = 1000000000f;
                GameObject ClosestElement = null;

                foreach (GameObject element in UIElementsToAssign)
                {
                    float DistToElement = Math.Abs(Vector3.Distance(Pair.Value.DivisionGO.transform.position, element.transform.position));

                    if (DistToElement < ClosestDist)
                    {
                        ClosestDist = DistToElement;
                        ClosestElement = element;
                    }
                }

                Pair.Value.TempPos = ClosestElement.transform.position;
                UIElementsToAssign.Remove(ClosestElement);

                if (DistFromStartToCursor > MinDistance)
                {
                    TempWidth = (int)Math.Round(Pair.Value.MaxWidth * (DistFromStartToCursor / MaxDistance));

                    TempDepth = -(Pair.Value.SoldierList.Count / TempWidth);
                }

                if ((DistFromStartToCursor / MaxDistance) > 1f)
                {
                    TempWidth = Pair.Value.MaxWidth;
                    TempDepth = -(Pair.Value.SoldierList.Count / TempWidth);
                }

                if (DistFromStartToCursor <= MaxDistance && DistFromStartToCursor >= MinDistance)
                {
                    ClosestElement.transform.localScale = new Vector3(0.1f * TempWidth, 1f, 0.1f * (Pair.Value.SoldierList.Count / TempWidth));

                    Pair.Value.TempWidth = TempWidth;

                    if (Pair.Value.SoldierList.Count % Pair.Value.Width != 0)
                    {
                        GameObject RemainderUIElement = GameObject.CreatePrimitive(PrimitiveType.Plane);

                        RemainderUIElement.transform.localScale = new Vector3(0.1f * (Pair.Value.SoldierList.Count % TempWidth), 1f, 0.1f);

                        RemainderUIElement.transform.position = new Vector3(ClosestElement.transform.position.x, ClosestElement.transform.position.y, ClosestElement.transform.position.z + ((TempDepth / 2) - 0.5f));

                        RemainderUIElement.transform.SetParent(ClosestElement.transform);

                        MeshRenderer RemainderRenderer = RemainderUIElement.GetComponent<MeshRenderer>();

                        RemainderRenderer.material = mat;
                        RemainderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                else if (DistFromStartToCursor > MaxDistance)
                {
                    ClosestElement.transform.localScale = new Vector3(0.1f * Pair.Value.MaxWidth, 1f, 0.1f * (Pair.Value.SoldierList.Count / Pair.Value.MaxWidth));

                    Pair.Value.TempWidth = TempWidth;

                    if (Pair.Value.SoldierList.Count % Pair.Value.MaxWidth * (DistFromStartToCursor / MaxDistance) != 0)
                    {
                        GameObject RemainderUIElement = GameObject.CreatePrimitive(PrimitiveType.Plane);

                        RemainderUIElement.transform.localScale = new Vector3(0.1f * (Pair.Value.SoldierList.Count % TempWidth), 1f, 0.1f);
                        RemainderUIElement.transform.position = new Vector3(ClosestElement.transform.position.x, ClosestElement.transform.position.y, ClosestElement.transform.position.z + (-(Pair.Value.SoldierList.Count / TempWidth) / 2f) - 0.5f);

                        RemainderUIElement.transform.SetParent(ClosestElement.transform);

                        MeshRenderer RemainderRenderer = RemainderUIElement.GetComponent<MeshRenderer>();

                        RemainderRenderer.material = mat;
                        RemainderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                else if (DistFromStartToCursor < MinDistance)
                {
                    ClosestElement.transform.localScale = new Vector3(0.1f * Pair.Value.MinWidth, 1f, 0.1f * (Pair.Value.SoldierList.Count / Pair.Value.MinWidth));

                    Pair.Value.TempWidth = Pair.Value.MinWidth;

                    if (Pair.Value.SoldierList.Count % Pair.Value.MinWidth != 0)
                    {
                        GameObject RemainderUIElement = GameObject.CreatePrimitive(PrimitiveType.Plane);

                        RemainderUIElement.transform.localScale = new Vector3(0.1f * (Pair.Value.SoldierList.Count % Pair.Value.MinWidth), 1f, 0.1f);
                        RemainderUIElement.transform.position = new Vector3(ClosestElement.transform.position.x, ClosestElement.transform.position.y, ClosestElement.transform.position.z + (-(Pair.Value.SoldierList.Count / Pair.Value.MinWidth) / 2f) - 0.5f);

                        RemainderUIElement.transform.SetParent(ClosestElement.transform);

                        MeshRenderer RemainderRenderer = RemainderUIElement.GetComponent<MeshRenderer>();

                        RemainderRenderer.material = mat;
                        RemainderRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }

                ClosestElement.transform.eulerAngles = new Vector3(ClosestElement.transform.eulerAngles.x, Angle + 90, ClosestElement.transform.eulerAngles.z);

                MeshRenderer UIelementRenderer = ClosestElement.GetComponent<MeshRenderer>();

                UIelementRenderer.material = mat;

                UIelementRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                ListofDivisionPlacementUIElements.Add(ClosestElement);
            }
        }
    }

    public float NewDivisionAngle()
    {
        Ray CursorRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        Ray StartPosRay = Camera.main.ScreenPointToRay(DivisionPlacementBoxStartPos);

        RaycastHit CurrentPosHit;
        RaycastHit StartPosHit;

        //They use binary instead of int for the layermask
        // << is shifting a bit one spot to the left
        Physics.Raycast(CursorRay, out CurrentPosHit, 1200, 1 << 1);
        Physics.Raycast(StartPosRay, out StartPosHit, 1200, 1 << 1);

        float angle = (Mathf.Atan2(StartPosHit.point.x - CurrentPosHit.point.x, StartPosHit.point.z - CurrentPosHit.point.z) * 180f / Mathf.PI);

        return angle;
    }

    public void CalculateMaxAndMinWidth(Division div)
    {
        try
        {
            //D >= W * 8
            //W >= D * 0.5f
            int SoldierCount = div.SoldierList.Count;

            int Depth = 0;
            int MaxWidth = SoldierCount;

            while (Depth * 8 < MaxWidth)
            {
                Depth++;
                MaxWidth = SoldierCount / Depth;
            }

            div.MaxWidth = MaxWidth;

            Depth = SoldierCount;
            int MinWidth = 0;

            while (2 * MinWidth < Depth)
            {
                MinWidth++;
                Depth = SoldierCount / MinWidth;
            }

            div.MinWidth = MinWidth;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void UpdateSoldierPositionsInFormationNew(Division div, Direction dir)
    {
        try
        {
            List<Vector2> NewPositions = new List<Vector2>();
            int OriginalDepth = (div.SoldierList.Count / div.Width) - 1;
            int Index = 0;
            int WidthIsEven = 0;
            int Y_Value = 0;
            bool FlipRows = false;

            if (div.TempWidth % 2 == 0)
                WidthIsEven = 1;

            if (div.SoldierList.Count % div.Width != 0)
                OriginalDepth++;

            int Difference = 0;

            int CurrentRowWidth = div.Width;
            int DesiredRowWidth = div.TempWidth;

            div.PreviousRemainder.Clear();

            float RemainderDepth = div.SoldierList.OrderByDescending(S => S.PositionInFormation.y).ToList()[0].PositionInFormation.y;

            foreach (Soldier sol in div.SoldierList.Where(S => S.PositionInFormation.y == RemainderDepth))
                div.PreviousRemainder.Add(sol);

            if (dir != Direction.Back)
                div.IsFlipped = false;
            else
                FlipRows = true;

            if (dir != Direction.Forward)
                FlipDivision2(div, dir);

            if (dir == Direction.Left || dir == Direction.Right)
                OriginalDepth = div.Width - 1;

            List<List<Soldier>> ListOfSoldierLists = new List<List<Soldier>>();

            List<Soldier> SoldiersInMainSection = new List<Soldier>();
            List<Soldier> SoldiersInOffsetSection = new List<Soldier>();

            List<List<Soldier>> OriginalOrder = new List<List<Soldier>>();

            for (int y = 0; y <= OriginalDepth; y++)
            {
                List<Soldier> Row = div.SoldierList.Where(S => S.PositionInFormation.y == y).OrderByDescending(S => S.PositionInFormation.y).ToList();
                OriginalOrder.Add(Row);
            }

            CurrentRowWidth = OriginalOrder[0].Count;

            if (CurrentRowWidth < DesiredRowWidth)
            {
                List<Soldier> CurrentSection = new List<Soldier>();
                List<Soldier> OffsetSection = new List<Soldier>();
                List<Soldier> CurrentRow = new List<Soldier>();
                List<Soldier> Remainder = new List<Soldier>();

                for (int y = 0; y < OriginalOrder.Count; y++)
                {
                    CurrentRow = OriginalOrder[y].OrderBy(S => S.PositionInFormation.x).ToList();

                    if (dir != Direction.Back && dir != Direction.Forward)
                        CurrentRowWidth = CurrentRow.Count;

                    if (Y_Value == 0)
                    {
                        for (int x = 0; x < CurrentRowWidth; x++)
                        {
                            CurrentRow[x].PositionInFormation = new Vector2(-(DesiredRowWidth / 2) + WidthIsEven + x, Y_Value);
                            CurrentSection.Add(CurrentRow[x]);
                            Index++;
                        }

                        Difference = DesiredRowWidth - CurrentSection.Count;

                        Y_Value++;
                    }
                    else
                    {
                        if (Difference > CurrentRowWidth)
                        {
                            ListOfSoldierLists.Add(CurrentSection);
                            ListOfSoldierLists.Add(OffsetSection);

                            CurrentSection = new List<Soldier>();
                            OffsetSection = new List<Soldier>();

                            int DiffStartPos = (DesiredRowWidth / 2) - Difference + 1;

                            for (int x = 0; x < CurrentRow.Count; x++)
                            {
                                CurrentRow[x].PositionInFormation = new Vector2(DiffStartPos + x, Y_Value - 1);
                                CurrentSection.Add(CurrentRow[x]);
                                Index++;
                            }

                            Difference = DesiredRowWidth - CurrentSection.Count - (DesiredRowWidth - Difference);
                        }
                        else
                        {
                            if (CurrentRow.Count < Difference)
                            {
                                foreach (Soldier sol in CurrentRow)
                                    Remainder.Add(sol);

                                break;
                            }

                            int DiffStartPos = (DesiredRowWidth / 2) - Difference + 1;

                            int i = 0;

                            if (CurrentRow.Count < CurrentRowWidth)
                                CurrentRowWidth = CurrentRow.Count;

                            for (int x = 0; x < Difference; x++)
                            {
                                CurrentRow[(CurrentRowWidth - Difference) + x].PositionInFormation = new Vector2(DiffStartPos + i, Y_Value - 1);
                                OffsetSection.Add(CurrentRow[(CurrentRowWidth - Difference) + x]);
                                Index++;
                                i++;
                            }

                            if (Index + DesiredRowWidth > div.SoldierList.Count)
                            {
                                for (int x = 0; x < (CurrentRowWidth - Difference); x++)
                                    Remainder.Add(CurrentRow[x]);

                                if (y + 1 < OriginalOrder.Count)
                                    for (int j = y + 1; j < OriginalOrder.Count; j++)
                                        foreach (Soldier sol in OriginalOrder[j])
                                            Remainder.Add(sol);

                                break;
                            }

                            for (int x = 0; x < (CurrentRowWidth - Difference); x++)
                            {
                                CurrentRow[x].PositionInFormation = new Vector2(-(DesiredRowWidth / 2) + WidthIsEven + x, Y_Value);
                                CurrentSection.Add(CurrentRow[x]);
                                Index++;
                            }

                            Difference = DesiredRowWidth - (CurrentRowWidth - Difference);

                            Y_Value++;
                        }
                    }
                }

                if (CurrentSection.Count > 0)
                    ListOfSoldierLists.Add(CurrentSection);

                if (OffsetSection.Count > 0)
                    ListOfSoldierLists.Add(OffsetSection);

                if (Remainder.Count > 0)
                {
                    float XOffset = 0;

                    if ((Remainder.Count % 2 == 0 && DesiredRowWidth % 2 != 0) || (Remainder.Count % 2 != 0 && DesiredRowWidth % 2 == 0))
                    {
                        if (DesiredRowWidth % 2 != 0)
                            XOffset = 0.5f;
                        else
                            XOffset = -0.5f;
                    }

                    Remainder = Remainder.OrderBy(S => S.PositionInFormation.x).ToList();

                    for (int x = 0; x < Remainder.Count; x++)
                        Remainder[x].PositionInFormation = new Vector2(-(Remainder.Count / 2) + WidthIsEven + x + XOffset, Y_Value);

                    ListOfSoldierLists.Add(Remainder);
                }
            }
            else if (CurrentRowWidth > DesiredRowWidth)
            {
                int DesiredRowIsEven = 0;

                List<Soldier> RemainderFromLastRow = new List<Soldier>();

                if (DesiredRowWidth % 2 == 0)
                    DesiredRowIsEven = 1;

                Y_Value = 0;

                for (int y = 0; y <= OriginalDepth; y++)
                {
                    List<Soldier> CurrentRow = OriginalOrder[y];

                    while (RemainderFromLastRow.Count >= DesiredRowWidth)
                    {
                        ListOfSoldierLists.Add(SoldiersInMainSection);
                        ListOfSoldierLists.Add(SoldiersInOffsetSection);

                        SoldiersInMainSection = new List<Soldier>();
                        SoldiersInOffsetSection = new List<Soldier>();

                        for (int x = 0; x < DesiredRowWidth; x++)
                        {
                            RemainderFromLastRow[0].PositionInFormation = new Vector2((-(DesiredRowWidth / 2) + DesiredRowIsEven) + x, Y_Value);

                            SoldiersInMainSection.Add(RemainderFromLastRow[0]);
                            RemainderFromLastRow.Remove(RemainderFromLastRow[0]);
                        }

                        ListOfSoldierLists.Add(SoldiersInMainSection);
                        SoldiersInMainSection = new List<Soldier>();

                        Y_Value++;
                    }

                    if (CurrentRow.Count + RemainderFromLastRow.Count < DesiredRowWidth)
                    {
                        foreach (Soldier sol in CurrentRow)
                            RemainderFromLastRow.Add(sol);

                        break;
                    }

                    int SpotsForCurrentRow = (DesiredRowWidth - RemainderFromLastRow.Count);

                    for (int x = 0; x < SpotsForCurrentRow; x++)
                    {
                        CurrentRow[x].PositionInFormation = new Vector2((-(DesiredRowWidth / 2) + DesiredRowIsEven) + x, Y_Value);

                        SoldiersInMainSection.Add(CurrentRow[x]);
                    }

                    for (int x = 0; x < RemainderFromLastRow.Count; x++)
                    {
                        RemainderFromLastRow[x].PositionInFormation = new Vector2((-(DesiredRowWidth / 2) + DesiredRowIsEven) + SpotsForCurrentRow + x, Y_Value);

                        SoldiersInOffsetSection.Add(RemainderFromLastRow[x]);
                    }

                    RemainderFromLastRow.Clear();

                    for (int x = SpotsForCurrentRow; x < CurrentRow.Count; x++)
                    {
                        RemainderFromLastRow.Add(CurrentRow[x]);
                    }

                    Y_Value++;
                }


                if (SoldiersInMainSection.Count > 0)
                    ListOfSoldierLists.Add(SoldiersInMainSection);

                if (SoldiersInOffsetSection.Count > 0)
                    ListOfSoldierLists.Add(SoldiersInOffsetSection);

                if (RemainderFromLastRow.Count > 0)
                {
                    float RemainderWidthIsEven = 0f;
                    float XOffset = 0f;

                    if (RemainderFromLastRow.Count > DesiredRowWidth)
                    {
                        RemainderWidthIsEven = DesiredRowIsEven;
                    }
                    else
                    {
                        if (RemainderFromLastRow.Count % 2 == 0)
                        {
                            if (DesiredRowIsEven == 0)
                                XOffset = -0.5f;

                            RemainderWidthIsEven = 1f;
                        }

                        if (DesiredRowIsEven == 1 && RemainderFromLastRow.Count % 2 != 0)
                            XOffset = 0.5f;
                    }

                    RemainderFromLastRow = RemainderFromLastRow.OrderBy(S => S.PositionInFormation.x).ToList();

                    int x = 0;
                    int StartingPoint = 0;

                    if (RemainderFromLastRow.Count < DesiredRowWidth)
                        StartingPoint = -(RemainderFromLastRow.Count / 2);
                    else
                        StartingPoint = -(DesiredRowWidth / 2);

                    for (int i = 0; i < RemainderFromLastRow.Count; i++)
                    {
                        RemainderFromLastRow[i].PositionInFormation = new Vector2(StartingPoint + x + RemainderWidthIsEven + XOffset, Y_Value);
                        x++;

                        if (x >= DesiredRowWidth)
                        {
                            x = 0;
                            Y_Value++;

                            if ((RemainderFromLastRow.Count - i) < DesiredRowWidth)
                            {
                                StartingPoint = -((RemainderFromLastRow.Count - i) / 2);

                                if ((RemainderFromLastRow.Count - i) % 2 == 0)
                                {
                                    if (DesiredRowIsEven == 0)
                                        XOffset = -0.5f;

                                    RemainderWidthIsEven = 1f;
                                }

                                if (DesiredRowIsEven == 1 && (RemainderFromLastRow.Count - i) % 2 == 0)
                                    XOffset = 0.5f;
                            }
                            else
                            {
                                StartingPoint = -(DesiredRowWidth / 2);
                            }
                        }
                    }

                    ListOfSoldierLists.Add(RemainderFromLastRow);
                }

                Index = 1000;
            }


            DesiredRowWidth = div.TempWidth;

            int Temp = 0;

            foreach (List<Soldier> SoldierList in ListOfSoldierLists)
                Temp += SoldierList.Count;

            if (Temp != div.SoldierList.Count && CurrentRowWidth != DesiredRowWidth)
            {
                Debug.Log("Major Bug!!!!!!");
                //Time.timeScale = 0;
            }

            if (CurrentRowWidth < DesiredRowWidth)
            {
                StartCoroutine(StaggerMarch2(ListOfSoldierLists, div, FlipRows));
            }
            else if (CurrentRowWidth == DesiredRowWidth)
            {
                foreach (Soldier soldier in div.SoldierList)
                {
                    soldier.SoldierDestination = div.DivisionGO.transform.TransformPoint(new Vector3(soldier.PositionInFormation.x, 0f, -soldier.PositionInFormation.y));
                    soldier.NavAgent.SetDestination(soldier.SoldierDestination);

                    if (soldier.NavAgent.isStopped)
                        soldier.NavAgent.isStopped = false;
                }

                FunctionsToRunInChildThread.Add(() => SetAnimations(AnimationType.Walk, div.SoldierList));
            }
            else
                StartCoroutine(StaggerMarch2(ListOfSoldierLists, div, FlipRows));

            div.CurrentDirection = dir;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void SetAnimations(AnimationType AnimType, List<Soldier> SoldierList)
    {
        try
        {
            if (SoldierList.Count == 0)
                return;

            //The following is based on the assumption that all units in a division have the same animation list
            List<Animation> AnimationList = SoldierList[0].SubType.AnimationList.Where(A => A.Type == AnimType).ToList();

            List<Soldier> SoldiersToUpdate = SoldierList.Where(S => !AnimationList.Contains(S.AnimState) && S.IsAlive).ToList();

            if (SoldiersToUpdate.Count == 0)
                return;

            Action action = () =>
            {
                foreach (Soldier sol in SoldiersToUpdate)
                {
                    sol.AnimState = sol.SubType.AnimationList.Where(A => A.Type == AnimType).ToList()[0];

                    foreach (Transform child in sol.SoldierGO.transform)
                    {
                        if (child.name == sol.rank.ToString() + " " + sol.state.ToString() + " " + sol.AnimState.AnimationName)
                        {
                            child.gameObject.SetActive(true);

                            MeshRenderer renderer = child.gameObject.GetComponent<MeshRenderer>();

                            renderer.material.SetFloat(renderer.material.shader.GetPropertyName(7), 1);
                            renderer.material.SetFloat(renderer.material.shader.GetPropertyName(8), Shader.GetGlobalVector("_Time").y);
                        }
                        else if (child.name == "Cone")
                            continue;
                        else
                            child.gameObject.SetActive(false);
                    }
                }
            };

            lock (FunctionsToRunInMainThread)
            {
                FunctionsToRunInMainThread.Add(action);
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void SetCombatAnimations(Division div)
    {
        try
        {
            //Debug.Log(div.DivisionGO.name);

            List<Soldier> SoldiersInCombat = new List<Soldier>();

            //Is position in formation a valid way to do this?
            foreach (Direction dir in div.FlanksUnderAttack)
            {
                switch (dir)
                {
                    case Direction.Forward:
                        foreach (Soldier sol in div.SoldierList.Where(S => S.PositionInFormation.y == 0 && S.IsAlive))
                            SoldiersInCombat.Add(sol);

                        break;
                    case Direction.Back:
                        float MaxDepth = div.SoldierList.OrderByDescending(S => S.PositionInFormation.y).ToList()[0].PositionInFormation.y;

                        foreach (Soldier sol in div.SoldierList.Where(S => S.PositionInFormation.y == MaxDepth && S.IsAlive))
                            SoldiersInCombat.Add(sol);

                        break;
                    case Direction.Left:
                        float X_Value = div.SoldierList.OrderBy(S => S.PositionInFormation.x).ToList()[0].PositionInFormation.x;

                        foreach (Soldier sol in div.SoldierList.Where(S => S.PositionInFormation.x == X_Value && S.IsAlive))
                            SoldiersInCombat.Add(sol);

                        break;
                    case Direction.Right:
                        X_Value = div.SoldierList.OrderByDescending(S => S.PositionInFormation.x).ToList()[0].PositionInFormation.x;

                        foreach (Soldier sol in div.SoldierList.Where(S => S.PositionInFormation.x == X_Value && S.IsAlive))
                            SoldiersInCombat.Add(sol);

                        break;
                }
            }

            System.Random R = new System.Random();

            foreach (Soldier sol in SoldiersInCombat.Where(S => S.AnimationTimer.Enabled == false))
            {
                Animation Anim = sol.SubType.AnimationList[0];

                //20% chance of attack
                if (R.Next(1, 101) > 80)
                {
                    float LargestDist = 10000f;
                    Trigger.Detection detection = new Trigger.Detection(null, null, null);
                    Soldier DetectedSoldier = null;

                    if (sol.trig.DetectedObjects.Count == 0)
                        continue;

                    foreach (Trigger.Detection detect in sol.trig.DetectedObjects)
                    {
                        float Distance = Vector3.Distance(sol.SoldierGO.transform.position, detect.GO.transform.position);

                        if (Distance < LargestDist && detect.DetectedSoldier.IsAlive)
                        {
                            LargestDist = Distance;
                            detection = detect;
                            DetectedSoldier = detect.DetectedSoldier;
                        }
                    }

                    if (DetectedSoldier == null)
                        continue;

                    if (DetectedSoldier.IsAlive == false)
                        continue;

                    List<Animation> PossibleAnimations = new List<Animation>();

                    if (R.Next(1, 101) <= DetectedSoldier.SubType.EvasionChance)
                    {
                        //Block or in the case of the Celt we could use lean back

                        PossibleAnimations = DetectedSoldier.SubType.AnimationList.Where(A => A.Type == AnimationType.Block).ToList();

                        Anim = PossibleAnimations[R.Next(0, PossibleAnimations.Count - 1)];
                    }
                    else
                    {
                        //Hit or Death
                        //Are we doing Attack - Armor = Damage or Attack * (Armor / 100)  = Damage

                        DetectedSoldier.Health = DetectedSoldier.Health - (sol.SubType.Attack - DetectedSoldier.SubType.Armor);

                        if (DetectedSoldier.Health < 0f)
                        {
                            DeathTimerLogic(DetectedSoldier);

                            PossibleAnimations = sol.SubType.AnimationList.Where(A => A.Type == AnimationType.Attack).ToList();

                            TimerLogic(PossibleAnimations[R.Next(0, PossibleAnimations.Count - 1)], sol);

                            continue;
                        }
                        else
                        {
                            PossibleAnimations = DetectedSoldier.SubType.AnimationList.Where(A => A.Type == AnimationType.Hit).ToList();

                            Anim = PossibleAnimations[R.Next(0, PossibleAnimations.Count - 1)];
                        }
                    }

                    TimerLogic(Anim, DetectedSoldier);

                    PossibleAnimations = sol.SubType.AnimationList.Where(A => A.Type == AnimationType.Attack).ToList();

                    TimerLogic(PossibleAnimations[R.Next(0, PossibleAnimations.Count - 1)], sol);
                }
                else
                {
                    //Combat Idle (or whatever). Set the Animation Timer though
                    Anim = sol.SubType.AnimationList.Where(A => A.Type == AnimationType.CombatIdle).ToList()[0];
                    TimerLogic(Anim, sol);
                }
            }

            List<Soldier> Temp = div.SoldierList.Except(SoldiersInCombat).Where(S => S.AnimationTimer.Enabled == false && S.IsAlive && S.AnimState.Type != AnimationType.CombatIdle).ToList();

            //When we do the below, we recieve an error saying that the FunctionsToRunInMainThread Collection was modified
            //That's cause this is a function being run from that collection
            if (Temp.Count > 0)
                SetAnimations(AnimationType.CombatIdle, Temp);

            //But doing this is totally fine
            //if (Temp.Count > 0)
            //    foreach (Soldier sol in Temp)
            //        SetAnimations(AnimationType.CombatIdle, null, sol);
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public bool LinesIntersect(Vector2 A, Vector2 B, Vector2 C, Vector2 D)
    {
        //Determines whether line AB intersects CD
        Vector2 Cmp = new Vector2(C.x - A.x, C.y - A.y);
        Vector2 r = new Vector2(B.x - A.x, B.y - A.y);
        Vector2 s = new Vector2(D.x - C.x, D.y - C.y);

        float CmPxr = Cmp.x * r.y - Cmp.y * r.x;
        float CmPxs = Cmp.x * s.y - Cmp.y * s.x;
        float RxS = r.x * s.y - r.y * s.x;

        //lines are collinear, and so intersect if they have any overlap
        if (CmPxr == 0f)
            return ((C.x - A.x < 0f) != (C.x - B.x < 0f)) || ((C.y - A.y < 0f) != (C.y - B.y < 0f));

        //lines are parallel
        if (RxS == 0f)
            return false;

        float rxsr = 1f / RxS;
        float T = CmPxs * rxsr;
        float U = CmPxr * rxsr;

        return (T >= 0f) && (T <= 1f) && (U >= 0f) && (U <= 1f);
    }

    private IEnumerator StaggerMarch2(List<List<Soldier>> ListOfSoldierLists, Division div, bool FlippedRows = false)
    {
        float YOffset = (div.SoldierList.Count / div.TempWidth) / 2;
        float DelayModifier = 0f;

        float WidthRatio = (float)div.TempWidth / (float)div.Width;

        DelayModifier = 1.2f;


        UpdateDivisionCorners(div);

        List<List<Soldier>> ListOfLists = new List<List<Soldier>>();

        foreach (List<Soldier> SoldierList in ListOfSoldierLists)
            if (SoldierList.Count > 0)
                ListOfLists.Add(SoldierList);

        float DistAnswer = 0f;

        float AverageDistanceFromDestination = 0f;
        List<float> AverageDistanceFromDestinationPerGroup = new List<float>();

        foreach (List<Soldier> SolList in ListOfLists)
        {
            float AverageDistForGroup = 0f;

            foreach (Soldier sol in SolList)
            {
                sol.SoldierDestination = div.DivisionGO.transform.TransformPoint(new Vector3(sol.PositionInFormation.x, 0f, (-sol.PositionInFormation.y) + YOffset));

                DistAnswer = Vector3.Distance(sol.SoldierGO.transform.position, sol.SoldierDestination);

                AverageDistanceFromDestination += DistAnswer;
                AverageDistForGroup += DistAnswer;
            }

            AverageDistanceFromDestinationPerGroup.Add(AverageDistForGroup / SolList.Count);
        }

        AverageDistanceFromDestination = AverageDistanceFromDestination / div.SoldierList.Count;

        int ListIndex = 0;
        int LastYValue = 0;

        foreach (List<Soldier> SoldierList in ListOfLists)
        {
            //The closer a group is to the goal the longer the delay should be and vise versa
            float InstancedDelayModifier = AverageDistanceFromDestinationPerGroup[ListIndex] / AverageDistanceFromDestination;

            //This should probably vary depending on width
            if (LastYValue < SoldierList[0].PositionInFormation.y)
                InstancedDelayModifier -= 0.3f;

            foreach (Soldier sol in SoldierList.Where(S => S.IsAlive))
            {
                sol.NavAgent.SetDestination(sol.SoldierDestination);
                sol.IsInMotion = true;

                if (sol.NavAgent.isStopped)
                    sol.NavAgent.isStopped = false;

                SetAnimations(AnimationType.Walk, new List<Soldier> { sol });

                LastYValue = (int)sol.PositionInFormation.y;
            }

            ListIndex++;
            yield return new WaitForSeconds(InstancedDelayModifier * DelayModifier);
        }
    }

    private void FlipDivision2(Division div, Direction dir)
    {
        int DivDepth = 0;
        int DivWidth = 0;
        int CurrentWidthIsEven = 0;

        int MaxXValue = div.Width / 2;
        int MinXValue = -(div.Width / 2) - CurrentWidthIsEven;

        float XOffset = 0f;

        List<List<Soldier>> ListOfRows = new List<List<Soldier>>();

        switch (dir)
        {
            case Direction.Back:
                List<float> RemainderXValues = new List<float>();

                DivDepth = div.SoldierList.Count / div.Width - 1;

                div.IsFlipped = true;

                if (div.Width % 2 == 0)
                    CurrentWidthIsEven = 1;

                if (div.SoldierList.Count % div.Width != 0)
                    DivDepth++;

                List<Soldier> OGRemainder = div.SoldierList.Where(S => S.PositionInFormation.y == DivDepth).ToList();

                foreach (Soldier sol in OGRemainder)
                {
                    if ((OGRemainder.Count % 2 == 0 && div.Width % 2 != 0) || OGRemainder.Count % 2 != 0 && div.Width == 0)
                    {
                        RemainderXValues.Add(sol.PositionInFormation.x + 0.5f);
                        XOffset = 0.5f;
                    }
                    else
                        RemainderXValues.Add(sol.PositionInFormation.x);
                }

                for (int y = 0; y <= DivDepth; y++)
                    ListOfRows.Add(div.SoldierList.Where(S => S.PositionInFormation.y == (DivDepth - y)).ToList());

                for (int y = 0; y <= DivDepth; y++)
                {
                    List<Soldier> CurrentRow = ListOfRows[y];

                    for (int x = 0; x < CurrentRow.Count; x++)
                    {
                        int Diff = MaxXValue - (int)CurrentRow[x].PositionInFormation.x;

                        if (RemainderXValues.Contains((int)CurrentRow[x].PositionInFormation.x + XOffset))
                            CurrentRow[x].PositionInFormation = new Vector2(Diff + MinXValue, y);
                        else
                            CurrentRow[x].PositionInFormation = new Vector2(Diff + MinXValue, y - 1);
                    }
                }

                div.SoldierList = div.SoldierList.OrderBy(S => S.PositionInFormation.x).OrderBy(S => S.PositionInFormation.y).ToList();

                break;

            case Direction.Left:
                DivDepth = div.Width;
                DivWidth = div.SoldierList.Count / div.Width - 1;

                MinXValue = (int)div.SoldierList.OrderBy(S => S.PositionInFormation.x).ToList()[0].PositionInFormation.x;
                MaxXValue = (int)div.SoldierList.OrderByDescending(S => S.PositionInFormation.x).ToList()[0].PositionInFormation.x;

                if (DivWidth % 2 == 0)
                    CurrentWidthIsEven = 1;

                for (int y = 0; y <= DivDepth; y++)
                    ListOfRows.Add(div.SoldierList.Where(S => S.PositionInFormation.x == MinXValue + y).ToList());

                int Index = 0;

                for (int y = 0; y <= DivDepth; y++)
                {
                    List<Soldier> CurrentRow = ListOfRows[y].OrderByDescending(S => S.PositionInFormation.y).ToList();

                    for (int x = 0; x < CurrentRow.Count; x++)
                    {
                        int Diff = MaxXValue - (int)CurrentRow[x].PositionInFormation.x;

                        CurrentRow[x].PositionInFormation = new Vector2(-(DivWidth / 2) - CurrentWidthIsEven + x, y);

                        Index++;
                    }
                }

                div.SoldierList = div.SoldierList.OrderBy(S => S.PositionInFormation.x).OrderBy(S => S.PositionInFormation.y).ToList();

                break;

            case Direction.Right:
                DivDepth = div.Width;
                DivWidth = div.SoldierList.Count / div.Width - 1;

                MinXValue = (int)div.SoldierList.OrderBy(S => S.PositionInFormation.x).ToList()[0].PositionInFormation.x;
                MaxXValue = (int)div.SoldierList.OrderByDescending(S => S.PositionInFormation.x).ToList()[0].PositionInFormation.x;

                if (DivWidth % 2 == 0)
                    CurrentWidthIsEven = 1;

                for (int y = 0; y <= DivDepth; y++)
                    ListOfRows.Add(div.SoldierList.Where(S => S.PositionInFormation.x == MaxXValue - y).ToList());

                for (int y = 0; y < DivDepth; y++)
                {
                    List<Soldier> CurrentRow = ListOfRows[y].OrderByDescending(S => S.PositionInFormation.y).ToList();

                    for (int x = 0; x < CurrentRow.Count; x++)
                    {
                        int Diff = MinXValue + (int)CurrentRow[x].PositionInFormation.x;

                        CurrentRow[x].PositionInFormation = new Vector2((DivWidth / 2) + CurrentWidthIsEven - x, y);
                    }
                }

                div.SoldierList = div.SoldierList.OrderBy(S => S.PositionInFormation.x).OrderBy(S => S.PositionInFormation.y).ToList();

                break;
        }

    }

    public void AssignSoldiersToTheirDiv(Division div)
    {
        try
        {
            foreach (Soldier soldier in div.SoldierList)
                soldier.AssignedDivision = div;
        }
        catch (System.Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void EstablishDivisionBorders(Division div)
    {
        try
        {
            int DivDepth = (div.SoldierList.Count / div.Width) - 1;
            float XOffset = 0f;
            float YOffset = 0f;

            if (div.Width % 2 == 0)
                XOffset = 0.5f;

            if (div.SoldierList.Count % div.Width != 0)
            {
                DivDepth++;
                YOffset = 1;
            }

            List<Soldier> FirstRow = div.SoldierList.Where(S => S.PositionInFormation.y == 0).OrderByDescending(S => S.PositionInFormation.x).ToList();
            List<Soldier> LastFullRow = div.SoldierList.Where(S => S.PositionInFormation.y == (div.SoldierList.Count / div.Width) - 1).OrderByDescending(S => S.PositionInFormation.x).ToList();

            div.CornerSoldiers.Add(FirstRow[0]);
            div.CornerSoldiers.Add(FirstRow[FirstRow.Count - 1]);
            div.CornerSoldiers.Add(LastFullRow[0]);
            div.CornerSoldiers.Add(LastFullRow[LastFullRow.Count - 1]);

            float PositiveXPos = FirstRow[0].PositionInFormation.x;
            float NegativexPos = FirstRow[FirstRow.Count - 1].PositionInFormation.x;

            //div.BoundaryOffsets.Add(new Vector3(0.5f + XOffset, 0f, 0.5f));
            //div.BoundaryOffsets.Add(new Vector3(-0.5f + XOffset, 0f, 0.5f));
            //div.BoundaryOffsets.Add(new Vector3(0.5f + XOffset, 0f, -YOffset - 0.5f));
            //div.BoundaryOffsets.Add(new Vector3(-0.5f + XOffset, 0f, -YOffset - 0.5f));

            div.Boundaries.Add(FirstRow[0].SoldierGO.transform.position);
            div.Boundaries.Add(FirstRow[FirstRow.Count - 1].SoldierGO.transform.position);
            div.Boundaries.Add(LastFullRow[0].SoldierGO.transform.position);
            div.Boundaries.Add(LastFullRow[LastFullRow.Count - 1].SoldierGO.transform.position);
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void UpdateDivisionCorners(Division div)
    {
        //Okay but like why?
        int DivDepth = (div.SoldierList.Count / div.TempWidth) - 1;

        //div.BoundaryOffsets.Clear();
        div.Boundaries.Clear();
        div.LastCornerSoldiersList.Clear();

        //Were using the Corner Soldier List to reference back to the Soldier List
        //so that Last Corner Soldiers is referencing the main list and not the original Corner Soldier List
        foreach (Soldier sol in div.CornerSoldiers)
            div.LastCornerSoldiersList.Add(div.SoldierList.Where(S => S == sol).ToList()[0]);

        div.CornerSoldiers.Clear();

        List<Soldier> FirstRow = div.SoldierList.Where(S => S.PositionInFormation.y == 0).OrderByDescending(S => S.PositionInFormation.x).ToList();
        List<Soldier> LastFullRow = new List<Soldier>();

        if (div.SoldierList.Count % div.TempWidth != 0)
        {
            LastFullRow = div.SoldierList.Where(S => S.PositionInFormation.y == DivDepth).OrderByDescending(S => S.PositionInFormation.x).ToList();
        }
        else
            LastFullRow = div.SoldierList.Where(S => S.PositionInFormation.y == DivDepth).OrderByDescending(S => S.PositionInFormation.x).ToList();

        div.CornerSoldiers.Add(FirstRow[0]);
        div.CornerSoldiers.Add(FirstRow[FirstRow.Count - 1]);
        div.CornerSoldiers.Add(LastFullRow[0]);
        div.CornerSoldiers.Add(LastFullRow[LastFullRow.Count - 1]);

        div.Boundaries.Add(FirstRow[0].SoldierGO.transform.position);
        div.Boundaries.Add(FirstRow[FirstRow.Count - 1].SoldierGO.transform.position);
        div.Boundaries.Add(LastFullRow[0].SoldierGO.transform.position);
        div.Boundaries.Add(LastFullRow[LastFullRow.Count - 1].SoldierGO.transform.position);
    }

    public void DisplayDivisionBoundaries3()
    {
        try
        {
            foreach (Division div in DivisionList)
            {
                int RemainderYValue = (int)div.SoldierList.OrderByDescending(S => S.PositionInFormation.y).ToList()[0].PositionInFormation.y;

                Soldier TopLeftSoldier = div.CornerSoldiers[1];
                Soldier TopRightSoldier = div.CornerSoldiers[0];

                Soldier BackLeftSoldier = div.SoldierList.OrderBy(S => S.PositionInFormation.x).OrderByDescending(S => S.PositionInFormation.y).ToList()[0];
                Soldier BackRightSoldier = div.SoldierList.OrderByDescending(S => S.PositionInFormation.x).OrderByDescending(S => S.PositionInFormation.y).ToList()[0];

                Soldier RightTopSoldier = div.CornerSoldiers[0];
                Soldier RightBottomSoldier = div.CornerSoldiers[2];

                Soldier LeftTopSoldier = div.CornerSoldiers[1];
                Soldier LeftBottomSoldier = div.CornerSoldiers[3];

                //Debug.Log(div.Width);

                List<Soldier> CurrentRemainder = div.SoldierList.Where(S => S.PositionInFormation.y == RemainderYValue).ToList();
                List<Soldier> FrontRow = div.SoldierList.Where(S => S.PositionInFormation.y == 0).OrderBy(S => S.PositionInFormation.x).ToList();

                switch (div.CurrentDirection)
                {
                    case Direction.Forward:
                        if (div.LastCornerSoldiersList.Count != 0)
                        {
                            if (div.Width > div.LastWidth)
                                TopRightSoldier = div.LastCornerSoldiersList[0];
                            else
                                RightTopSoldier = div.LastCornerSoldiersList[0];

                            if (div.PreviousRemainder.Count < CurrentRemainder.Count)
                                BackLeftSoldier = div.PreviousRemainder[0];
                            else
                                LeftBottomSoldier = div.LastCornerSoldiersList[3];
                        }
                        break;

                    case Direction.Back:
                        BackLeftSoldier = CurrentRemainder[0];
                        BackRightSoldier = CurrentRemainder[CurrentRemainder.Count - 1];

                        LeftTopSoldier = FrontRow[0];

                        if (CurrentRemainder.Count < div.PreviousRemainder.Count)
                            RightBottomSoldier = div.LastCornerSoldiersList[1];
                        else
                            RightBottomSoldier = div.CornerSoldiers[2];

                        if (FrontRow.Contains(TopLeftSoldier))
                            TopLeftSoldier = div.PreviousRemainder[div.PreviousRemainder.Count - 1];

                        if (div.PreviousRemainder.Count + ((div.LastWidth - div.PreviousRemainder.Count) / 2) > div.Width)
                            TopRightSoldier = div.CornerSoldiers[0];
                        else
                            TopRightSoldier = div.PreviousRemainder[0];

                        if (div.Width > div.LastWidth)
                        {
                            RightTopSoldier = FrontRow[FrontRow.Count - 1];
                            LeftBottomSoldier = div.CornerSoldiers[3];
                        }
                        else if (div.Width == div.LastWidth)
                        {
                            LeftTopSoldier = div.CornerSoldiers[2];
                            RightTopSoldier = div.CornerSoldiers[3];
                            LeftBottomSoldier = div.CornerSoldiers[0];
                            RightBottomSoldier = div.CornerSoldiers[1];

                            TopLeftSoldier = div.PreviousRemainder[div.PreviousRemainder.Count - 1];
                        }
                        else
                        {
                            RightTopSoldier = div.LastCornerSoldiersList[3];
                            LeftBottomSoldier = div.LastCornerSoldiersList[0];
                        }
                        break;

                    case Direction.Left:
                        TopLeftSoldier = div.CornerSoldiers[1];
                        TopRightSoldier = div.SoldierList.Where(S => S.PositionInFormation.y == 0).OrderBy(S => S.PositionInFormation.x).ToList()[0];

                        LeftTopSoldier = div.LastCornerSoldiersList[3];
                        TopRightSoldier = div.LastCornerSoldiersList[1];

                        BackLeftSoldier = CurrentRemainder[0];
                        BackRightSoldier = CurrentRemainder[CurrentRemainder.Count - 1];
                        break;

                    case Direction.Right:
                        TopLeftSoldier = div.CornerSoldiers[1];
                        TopRightSoldier = div.LastCornerSoldiersList[2];
                        //TopRightSoldier = div.SoldierList.Where(S => S.PositionInFormation.y == 0).OrderBy(S => S.PositionInFormation.x).ToList()[0];

                        BackLeftSoldier = CurrentRemainder[0];
                        BackRightSoldier = CurrentRemainder[CurrentRemainder.Count - 1];
                        break;
                }

                //if (!SwitchBoundaryDisplay)
                //{
                //    Debug.DrawLine(TopLeftSoldier.SoldierGO.transform.position, TopRightSoldier.SoldierGO.transform.position, Color.white);
                //    Debug.DrawLine(BackLeftSoldier.SoldierGO.transform.position, BackRightSoldier.SoldierGO.transform.position, Color.magenta);
                //    Debug.DrawLine(LeftTopSoldier.SoldierGO.transform.position, LeftBottomSoldier.SoldierGO.transform.position, Color.green);
                //    Debug.DrawLine(RightTopSoldier.SoldierGO.transform.position, RightBottomSoldier.SoldierGO.transform.position, Color.blue);
                //}


                //Define Current Slopes and Intercepts
                float Front_Slope = ((float)Math.Round(TopRightSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(TopLeftSoldier.SoldierGO.transform.position.z, 1)) / ((float)Math.Round(TopRightSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(TopLeftSoldier.SoldierGO.transform.position.x, 1));
                float Front_Intercept = -((Front_Slope * TopLeftSoldier.SoldierGO.transform.position.x) - TopLeftSoldier.SoldierGO.transform.position.z);

                float Back_Slope = ((float)Math.Round(BackRightSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(BackLeftSoldier.SoldierGO.transform.position.z, 1)) / ((float)Math.Round(BackRightSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(BackLeftSoldier.SoldierGO.transform.position.x, 1));
                float Back_Intercept = -((Back_Slope * BackLeftSoldier.SoldierGO.transform.position.x) - BackLeftSoldier.SoldierGO.transform.position.z);

                float Left_Slope = ((float)Math.Round(LeftTopSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(LeftBottomSoldier.SoldierGO.transform.position.z, 1)) / ((float)Math.Round(LeftTopSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(LeftBottomSoldier.SoldierGO.transform.position.x, 1));
                float Left_Intercept = -((Left_Slope * LeftTopSoldier.SoldierGO.transform.position.x) - LeftTopSoldier.SoldierGO.transform.position.z);

                float Right_Slope = ((float)Math.Round(RightTopSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(RightBottomSoldier.SoldierGO.transform.position.z, 1)) / ((float)Math.Round(RightTopSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(RightBottomSoldier.SoldierGO.transform.position.x, 1));
                float Right_Intercept = -((Right_Slope * RightTopSoldier.SoldierGO.transform.position.x) - RightTopSoldier.SoldierGO.transform.position.z);

                //Extrapolate Correct Slopes and Intercepts
                float Combined_Intercepts = -Front_Intercept + Left_Intercept;
                float Combined_Slopes = Front_Slope - Left_Slope;
                float X = Combined_Intercepts / Combined_Slopes;
                float Z = Left_Slope * X + Left_Intercept;

                Vector3 FrontLeftCorner = new Vector3(X, 0f, Z);

                Combined_Intercepts = -Front_Intercept + Right_Intercept;
                Combined_Slopes = Front_Slope - Right_Slope;
                X = Combined_Intercepts / Combined_Slopes;
                Z = Right_Slope * X + Right_Intercept;

                Vector3 FrontRightCorner = new Vector3(X, 0f, Z);

                Combined_Intercepts = -Back_Intercept + Left_Intercept;
                Combined_Slopes = Back_Slope - Left_Slope;
                X = Combined_Intercepts / Combined_Slopes;
                Z = Left_Slope * X + Left_Intercept;

                Vector3 BackLeftCorner = new Vector3(X, 0f, Z);

                Combined_Intercepts = -Back_Intercept + Right_Intercept;
                Combined_Slopes = Back_Slope - Right_Slope;
                X = Combined_Intercepts / Combined_Slopes;
                Z = Right_Slope * X + Right_Intercept;

                Vector3 BackRightCorner = new Vector3(X, 0f, Z);

                //IsNaN Validation Checks
                if (double.IsNaN(FrontLeftCorner.x) || double.IsNaN(FrontLeftCorner.z))
                    FrontLeftCorner = new Vector3(LeftTopSoldier.SoldierGO.transform.position.x, 0f, TopLeftSoldier.SoldierGO.transform.position.z);
                if (double.IsNaN(FrontRightCorner.x) || double.IsNaN(FrontRightCorner.z))
                    FrontRightCorner = new Vector3(RightTopSoldier.SoldierGO.transform.position.x, 0f, TopRightSoldier.SoldierGO.transform.position.z);
                if (double.IsNaN(BackLeftCorner.x) || double.IsNaN(BackLeftCorner.z))
                    BackLeftCorner = new Vector3(LeftBottomSoldier.SoldierGO.transform.position.x, 0f, BackLeftSoldier.SoldierGO.transform.position.z);
                if (double.IsNaN(BackRightCorner.x) || double.IsNaN(BackRightCorner.z))
                    BackRightCorner = new Vector3(RightBottomSoldier.SoldierGO.transform.position.x, 0f, BackRightSoldier.SoldierGO.transform.position.z);

                //Ensure that all units are inside the boundaries
                List<Soldier> RightOutliers = new List<Soldier>();
                List<Soldier> LeftOutliers = new List<Soldier>();
                List<Soldier> FrontOutliers = new List<Soldier>();
                List<Soldier> BackOutliers = new List<Soldier>();

                int Counter = 0;

                FrontOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(TopLeftSoldier.SoldierGO.transform.localPosition, TopRightSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Forward) && S != TopLeftSoldier && S != TopRightSoldier).ToList();

                while (FrontOutliers.Count > 0)
                {
                    Soldier Outlier = DistanceFromLineAB2(FrontOutliers, TopLeftSoldier.SoldierGO.transform.localPosition, TopRightSoldier.SoldierGO.transform.localPosition);

                    if (Outlier.PositionInFormation.x < 0)
                        TopLeftSoldier = Outlier;
                    else
                        TopRightSoldier = Outlier;

                    //Should we think about switching slope to local as oppose to world pos?
                    Front_Slope = ((float)Math.Round(TopRightSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(TopLeftSoldier.SoldierGO.transform.position.z, 1)) / ((float)Math.Round(TopRightSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(TopLeftSoldier.SoldierGO.transform.position.x, 1));
                    Front_Intercept = -((Front_Slope * TopLeftSoldier.SoldierGO.transform.position.x) - TopLeftSoldier.SoldierGO.transform.position.z);

                    FrontOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(TopLeftSoldier.SoldierGO.transform.localPosition, TopRightSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Forward) && S != TopLeftSoldier && S != TopRightSoldier).ToList();

                    if (Counter > 20)
                        break;
                    else
                        Counter++;
                }

                if (Counter > 20)
                    Debug.Log("Front While Loop got stuck");

                //--------------------------------------------------------------------------------------------------------------------------------------------------

                Counter = 0;

                BackOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(BackLeftSoldier.SoldierGO.transform.localPosition, BackRightSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Back) && S != BackLeftSoldier && S != BackRightSoldier).ToList();

                List<Soldier> Last_Full_Row = div.SoldierList.Where(S => S.PositionInFormation.y == (div.SoldierList.Count / div.Width) - 1 && S.PositionInFormation.x > 0).ToList();

                Last_Full_Row = Last_Full_Row.OrderByDescending(S => S.PositionInFormation.x).ToList();

                foreach (Soldier sol in Last_Full_Row)
                {
                    FindDivisionOutlierNew(BackLeftSoldier.SoldierGO.transform.localPosition, BackRightSoldier.SoldierGO.transform.localPosition, sol.SoldierGO.transform.localPosition, Direction.Back);
                }

                float Center = (BackLeftSoldier.SoldierGO.transform.localPosition.x + BackRightSoldier.SoldierGO.transform.localPosition.x) / 2;

                while (BackOutliers.Count > 0)
                {
                    Soldier Outlier = DistanceFromLineAB2(BackOutliers, BackLeftSoldier.SoldierGO.transform.localPosition, BackRightSoldier.SoldierGO.transform.localPosition);

                    if (Outlier.SoldierGO.transform.localPosition.x < Center)
                        BackLeftSoldier = Outlier;
                    else
                        BackRightSoldier = Outlier;

                    Back_Slope = ((float)Math.Round(BackRightSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(BackLeftSoldier.SoldierGO.transform.transform.position.z, 1)) / ((float)Math.Round(BackRightSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(BackLeftSoldier.SoldierGO.transform.position.x, 1));
                    Back_Intercept = -((Back_Slope * BackLeftSoldier.SoldierGO.transform.position.x) - BackLeftSoldier.SoldierGO.transform.position.z);

                    List<Soldier> Taco21 = BackOutliers.Where(S => FindDivisionOutlierNew(BackLeftSoldier.SoldierGO.transform.localPosition, BackRightSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Back) && S != BackLeftSoldier && S != BackRightSoldier).ToList();

                    BackOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(BackLeftSoldier.SoldierGO.transform.localPosition, BackRightSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Back) && S != BackLeftSoldier && S != BackRightSoldier).ToList();

                    float AB_Angle = (float)Math.Atan2(BackRightSoldier.SoldierGO.transform.localPosition.z - BackLeftSoldier.SoldierGO.transform.localPosition.z, BackRightSoldier.SoldierGO.transform.localPosition.x - BackLeftSoldier.SoldierGO.transform.localPosition.x) * Mathf.Rad2Deg;

                    if (Counter > 20)
                        break;
                    else
                        Counter++;
                }

                if (Counter > 20)
                    Debug.Log("Back While Loop got stuck");

                Counter = 0;

                RightOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(RightTopSoldier.SoldierGO.transform.localPosition, RightBottomSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Right) && S != RightTopSoldier && S != RightBottomSoldier).ToList();

                Center = (RightTopSoldier.SoldierGO.transform.localPosition.z + RightBottomSoldier.SoldierGO.transform.localPosition.z) / 2;

                while (RightOutliers.Count > 0)
                {
                    Soldier Outlier = DistanceFromLineAB2(RightOutliers, RightTopSoldier.SoldierGO.transform.localPosition, RightBottomSoldier.SoldierGO.transform.localPosition);

                    if (Outlier.SoldierGO.transform.localPosition.z > Center)
                        RightTopSoldier = Outlier;
                    else
                        RightBottomSoldier = Outlier;

                    Right_Slope = ((float)Math.Round(RightTopSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(RightBottomSoldier.SoldierGO.transform.position.z, 1)) / ((float)Math.Round(RightTopSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(RightBottomSoldier.SoldierGO.transform.position.x, 1));
                    Right_Intercept = -((Right_Slope * RightTopSoldier.SoldierGO.transform.position.x) - RightTopSoldier.SoldierGO.transform.position.z);

                    RightOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(RightTopSoldier.SoldierGO.transform.localPosition, RightBottomSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Right) && S != RightTopSoldier && S != RightBottomSoldier).ToList();

                    if (Counter > 20)
                        break;
                    else
                        Counter++;
                }

                if (Counter > 20)
                    Debug.Log("Right While Loop got stuck");

                Counter = 0;

                LeftOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(LeftTopSoldier.SoldierGO.transform.localPosition, LeftBottomSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Left) && S != LeftTopSoldier && S != LeftBottomSoldier).ToList();

                Center = (LeftTopSoldier.SoldierGO.transform.localPosition.z + LeftBottomSoldier.SoldierGO.transform.localPosition.z) / 2;

                while (LeftOutliers.Count > 0)
                {
                    Soldier Outlier = DistanceFromLineAB2(LeftOutliers, LeftTopSoldier.SoldierGO.transform.localPosition, LeftBottomSoldier.SoldierGO.transform.localPosition);

                    if (Outlier.SoldierGO.transform.localPosition.z > Center)
                        LeftTopSoldier = Outlier;
                    else
                        LeftBottomSoldier = Outlier;

                    Left_Slope = ((float)Math.Round(LeftTopSoldier.SoldierGO.transform.position.z, 1) - (float)Math.Round(LeftBottomSoldier.SoldierGO.transform.position.z, 1)) / ((float)Math.Round(LeftTopSoldier.SoldierGO.transform.position.x, 1) - (float)Math.Round(LeftBottomSoldier.SoldierGO.transform.position.x, 1));
                    Left_Intercept = -((Left_Slope * LeftTopSoldier.SoldierGO.transform.position.x) - LeftTopSoldier.SoldierGO.transform.position.z);

                    LeftOutliers = div.SoldierList.Where(S => FindDivisionOutlierNew(LeftTopSoldier.SoldierGO.transform.localPosition, LeftBottomSoldier.SoldierGO.transform.localPosition, S.SoldierGO.transform.localPosition, Direction.Left) && S != LeftTopSoldier && S != LeftBottomSoldier).ToList();

                    if (Counter > 20)
                    {
                        Debug.Log("Left While Loop got stuck");
                        break;
                    }
                    else
                        Counter++;
                }

                bool Quack = FindDivisionOutlierNew(LeftTopSoldier.SoldierGO.transform.localPosition, LeftBottomSoldier.SoldierGO.transform.localPosition, div.CornerSoldiers[3].SoldierGO.transform.localPosition, Direction.Left);

                //Extrapolate Correct Slopes and Intercepts, Part 2
                Combined_Intercepts = -Front_Intercept + Left_Intercept;
                Combined_Slopes = Front_Slope - Left_Slope;
                X = Combined_Intercepts / Combined_Slopes;
                Z = Left_Slope * X + Left_Intercept;

                FrontLeftCorner = new Vector3(X, 0f, Z);

                Combined_Intercepts = -Front_Intercept + Right_Intercept;
                Combined_Slopes = Front_Slope - Right_Slope;
                X = Combined_Intercepts / Combined_Slopes;
                Z = Right_Slope * X + Right_Intercept;

                FrontRightCorner = new Vector3(X, 0f, Z);

                Combined_Intercepts = -Back_Intercept + Left_Intercept;
                Combined_Slopes = Back_Slope - Left_Slope;
                X = Combined_Intercepts / Combined_Slopes;
                Z = Left_Slope * X + Left_Intercept;

                BackLeftCorner = new Vector3(X, 0f, Z);

                Combined_Intercepts = -Back_Intercept + Right_Intercept;
                Combined_Slopes = Back_Slope - Right_Slope;
                X = Combined_Intercepts / Combined_Slopes;
                Z = Right_Slope * X + Right_Intercept;

                BackRightCorner = new Vector3(X, 0f, Z);


                //IsNaN Validation Checks, Part 2
                if (double.IsNaN(FrontLeftCorner.x) || double.IsNaN(FrontLeftCorner.z))
                {
                    //If one is horizontal and one is vertical 
                    if (double.IsInfinity(Front_Slope) && Left_Slope == 0)
                        FrontLeftCorner = new Vector3(TopLeftSoldier.SoldierGO.transform.position.x, 0f, LeftTopSoldier.SoldierGO.transform.position.z);

                    if (Front_Slope == 0 && double.IsInfinity(Left_Slope))
                        FrontLeftCorner = new Vector3(LeftTopSoldier.SoldierGO.transform.position.x, 0f, TopLeftSoldier.SoldierGO.transform.position.z);

                    if (Front_Slope == Left_Slope)
                    {
                        FrontLeftCorner = new Vector3((FrontRightCorner.x + BackLeftCorner.x) / 2, 0f, (FrontRightCorner.z + BackLeftCorner.z) / 2);
                        Debug.Log("Is the Front Left Corner Correct?");
                        //Unfreeze = false;
                    }

                    //If Front_Slope is horizontal or vertical and Left_Slope is not
                    if ((double.IsInfinity(Front_Slope) || Front_Slope == 0) && (Left_Slope != 0 && !double.IsInfinity(Left_Slope)))
                    {
                        if (double.IsInfinity(Front_Slope))
                        {
                            Debug.Log("Taco 2");
                            //rontRightCorner = new Vector3(TopRightSoldier.SoldierGO.transform.position.x, 0f, (Right_Slope * TopRightSoldier.SoldierGO.transform.position.x) + Right_Intercept);
                            //FrontLeftCorner = new Vector3((TopLeftSoldier.SoldierGO.transform.position.z - Left_Intercept) / Left_Slope, 0f, TopLeftSoldier.SoldierGO.transform.position.z);

                            FrontLeftCorner = new Vector3(TopLeftSoldier.SoldierGO.transform.position.x, 0f, (Left_Slope * TopLeftSoldier.SoldierGO.transform.position.x) + Left_Intercept);
                            //UnfreezeGame = false;
                        }
                    }

                    //If Left_Slope is horizontal or vertical and Front_Slope is not
                    if ((!double.IsInfinity(Front_Slope) && Front_Slope != 0) && (Left_Slope == 0 || double.IsInfinity(Left_Slope)))
                    {
                        if (Left_Slope == 0)
                        {
                            Debug.Log("Taco 3");
                        }

                        if (double.IsInfinity(Left_Slope))
                        {
                            //Debug.Log("Chunky");
                            FrontLeftCorner = new Vector3(LeftTopSoldier.SoldierGO.transform.position.x, 0f, (Front_Slope * LeftTopSoldier.SoldierGO.transform.position.x) + Front_Intercept);
                            //UnfreezeGame = false;
                        }
                    }
                }

                if (double.IsNaN(FrontRightCorner.x) || double.IsNaN(FrontRightCorner.z))
                {
                    //If one is horizontal and one is vertical
                    if (double.IsInfinity(Front_Slope) && Right_Slope == 0)
                        FrontRightCorner = new Vector3(TopRightSoldier.SoldierGO.transform.position.x, 0f, RightTopSoldier.SoldierGO.transform.position.z);

                    if (Front_Slope == 0 && double.IsInfinity(Right_Slope))
                        FrontRightCorner = new Vector3(RightTopSoldier.SoldierGO.transform.position.x, 0f, TopRightSoldier.SoldierGO.transform.position.z);

                    if (Front_Slope == Right_Slope)
                    {
                        FrontRightCorner = new Vector3((FrontLeftCorner.x + BackRightCorner.x) / 2, 0f, (FrontLeftCorner.x + BackRightCorner.x) / 2);
                        Debug.Log("Is the Front Right Corner correct?");
                        //UnfreezeGame = false;
                    }

                    //If Front_Slope is horizontal or vertical but Right_Slope is not
                    if ((double.IsInfinity(Front_Slope) || Front_Slope == 0) && (!double.IsInfinity(Right_Slope) && Right_Slope != 0))
                    {
                        if (double.IsInfinity(Front_Slope))
                        {
                            FrontRightCorner = new Vector3(TopRightSoldier.SoldierGO.transform.position.x, 0f, (Right_Slope * TopRightSoldier.SoldierGO.transform.position.x) + Right_Intercept);
                        }
                    }

                    //If Right_Slope is horizontal or vertical but Front_Slope is not
                    if ((!double.IsInfinity(Front_Slope) && Front_Slope != 0) && (double.IsInfinity(Right_Slope) || Right_Slope == 0))
                    {
                        if (double.IsInfinity(Right_Slope))
                        {
                            FrontRightCorner = new Vector3(RightTopSoldier.SoldierGO.transform.position.x, 0f, (Front_Slope * RightTopSoldier.SoldierGO.transform.position.x) + Front_Intercept);
                        }
                    }
                }

                if (double.IsNaN(BackLeftCorner.x) || double.IsNaN(BackLeftCorner.z))
                {
                    //If one is horizontal and one is vertical
                    if (double.IsInfinity(Back_Slope) && Left_Slope == 0)
                        BackLeftCorner = new Vector3(BackLeftSoldier.SoldierGO.transform.position.x, 0f, LeftBottomSoldier.SoldierGO.transform.position.z);

                    if (Back_Slope == 0 && double.IsInfinity(Left_Slope))
                        BackLeftCorner = new Vector3(LeftBottomSoldier.SoldierGO.transform.position.x, 0f, BackLeftSoldier.SoldierGO.transform.position.z);

                    if (Back_Slope == Left_Slope)
                        BackLeftCorner = new Vector3((FrontLeftCorner.x + BackRightCorner.x) / 2, 0f, (FrontLeftCorner.z + BackRightCorner.z) / 2);

                    if (double.IsInfinity(Back_Slope) && double.IsInfinity(Left_Slope))
                    {
                        //BackLeftCorner = new Vector3();
                        Debug.Log("Does this ever get hit?");
                        //UnfreezeGame = false;
                    }

                    //If Back_Slope is horizontal or vertical but Left_Slope is not
                    if ((double.IsInfinity(Back_Slope) || Back_Slope == 0) && (!double.IsInfinity(Left_Slope) && Left_Slope != 0))
                    {
                        if (Back_Slope == 0)
                        {
                            Debug.Log("Test 4");
                        }

                        if (double.IsInfinity(Back_Slope))
                        {
                            //Vector3 V1 = new Vector3(BackLeftSoldier.SoldierGO.transform.position.x, 0f, (Left_Slope * BackLeftSoldier.SoldierGO.transform.position.x) + Left_Intercept);
                            //Vector3 V2 = new Vector3(LeftBottomSoldier.SoldierGO.transform.position.x, 0f, (Left_Slope * LeftBottomSoldier.SoldierGO.transform.position.x) + Left_Intercept);

                            //if (V1.x != V2.x || V1.z != V2.z)
                            //    Debug.Log("Well they're definitely different");

                            BackLeftCorner = new Vector3(BackLeftSoldier.SoldierGO.transform.position.x, 0f, (Left_Slope * BackLeftSoldier.SoldierGO.transform.position.x) + Left_Intercept);

                            //BackLeftCorner = new Vector3(LeftBottomSoldier.SoldierGO.transform.position.x, 0f, (Left_Slope * LeftBottomSoldier.SoldierGO.transform.position.x) + Left_Intercept);
                            //UnfreezeGame = false;
                        }
                    }

                    //If Left_Slope is horizontal or vertical but Back_Slope is not
                    if ((!double.IsInfinity(Back_Slope) && Back_Slope != 0) && (double.IsInfinity(Left_Slope) || Left_Slope == 0))
                    {
                        if (Left_Slope == 0)
                        {
                            Debug.Log("Test 1");
                        }

                        if (double.IsInfinity(Left_Slope))
                        {
                            //Debug.Log("Monkey");
                            BackLeftCorner = new Vector3(LeftBottomSoldier.SoldierGO.transform.position.x, 0f, (Back_Slope * LeftBottomSoldier.SoldierGO.transform.position.x) + Back_Intercept);
                            //UnfreezeGame = false;
                        }
                    }
                }

                if (double.IsNaN(BackRightCorner.x) || double.IsNaN(BackRightCorner.z))
                {
                    //If one is horizontal and one is vertical


                    //if (double.IsInfinity(Back_Slope) && Left_Slope == 0)
                    //    BackLeftCorner = new Vector3(BackLeftSoldier.SoldierGO.transform.position.x, 0f, LeftBottomSoldier.SoldierGO.transform.position.z);

                    //if (Back_Slope == 0 && double.IsInfinity(Left_Slope))
                    //    BackLeftCorner = new Vector3(LeftBottomSoldier.SoldierGO.transform.position.x, 0f, BackLeftSoldier.SoldierGO.transform.position.z);


                    //This is wrong
                    if (double.IsInfinity(Back_Slope) && Right_Slope == 0)
                        BackRightCorner = new Vector3(BackRightSoldier.SoldierGO.transform.position.x, 0f, RightBottomSoldier.SoldierGO.transform.position.z);

                    if (Back_Slope == 0 && double.IsInfinity(Right_Slope))
                        BackRightCorner = new Vector3(RightBottomSoldier.SoldierGO.transform.position.x, 0f, BackRightSoldier.SoldierGO.transform.position.z);

                    if (Back_Slope == Right_Slope)
                    {
                        BackRightCorner = new Vector3((FrontRightCorner.x + BackLeftCorner.x) / 2, 0f, (FrontRightCorner.z + BackLeftCorner.z) / 2);
                        Debug.Log("Is the Back Right Corner right?");
                        //UnfreezeGame = false;
                    }

                    //If Back_Slope is horizontal or vertical but Right_Slope is not
                    if ((double.IsInfinity(Back_Slope) || Back_Slope == 0) && (!double.IsInfinity(Right_Slope) && Right_Slope != 0))
                    {
                        if (Back_Slope == 0)
                        {
                            Debug.Log("You are here 2");
                        }

                        if (double.IsInfinity(Back_Slope))
                        {
                            Debug.Log("Does this work 2");
                            //BackLeftCorner = new Vector3(BackLeftSoldier.SoldierGO.transform.position.x, 0f, (Left_Slope * BackLeftSoldier.SoldierGO.transform.position.x) + Left_Intercept);
                            BackRightCorner = new Vector3(BackRightSoldier.SoldierGO.transform.position.x, 0f, (Right_Slope * BackRightSoldier.SoldierGO.transform.position.x) + Right_Intercept);
                            //UnfreezeGame = false;
                        }
                    }

                    //If Right_Slope is horizontal or vertical but Back_Slope is not
                    if ((!double.IsInfinity(Back_Slope) && Back_Slope != 0) && (double.IsInfinity(Right_Slope) || Right_Slope == 0))
                    {
                        if (Right_Slope == 0)
                        {
                            Debug.Log("You are here");
                        }

                        if (double.IsInfinity(Right_Slope))
                        {
                            //Debug.Log("Does this work");
                            BackRightCorner = new Vector3(RightBottomSoldier.SoldierGO.transform.position.x, 0f, (Back_Slope * RightBottomSoldier.SoldierGO.transform.position.x) + Back_Intercept);
                            //UnfreezeGame = false;
                        }
                    }
                }

                //Visualizations
                if (SwitchBoundaryDisplay)
                {
                    Debug.DrawLine(FrontLeftCorner, FrontRightCorner, Color.magenta);
                    Debug.DrawLine(FrontLeftCorner, BackLeftCorner, Color.magenta);
                    Debug.DrawLine(FrontRightCorner, BackRightCorner, Color.magenta);
                    Debug.DrawLine(BackLeftCorner, BackRightCorner, Color.magenta);
                }
                else
                {
                    //Debug.DrawLine(TopLeftSoldier.SoldierGO.transform.position, TopRightSoldier.SoldierGO.transform.position, Color.green);
                    //Debug.DrawLine(BackLeftSoldier.SoldierGO.transform.position, BackRightSoldier.SoldierGO.transform.position, Color.green);
                    //Debug.DrawLine(LeftTopSoldier.SoldierGO.transform.position, LeftBottomSoldier.SoldierGO.transform.position, Color.green);
                    //Debug.DrawLine(RightTopSoldier.SoldierGO.transform.position, RightBottomSoldier.SoldierGO.transform.position, Color.green);
                }


                if (UnfreezeGame)
                    Time.timeScale = 1;
                else
                    Time.timeScale = 0;

                //Estalish Boundaries
                div.Boundaries.Clear();

                div.Boundaries.Add(FrontRightCorner);
                div.Boundaries.Add(FrontLeftCorner);
                div.Boundaries.Add(BackRightCorner);
                div.Boundaries.Add(BackLeftCorner);

                //EstablishDetectionZones(div, FrontLeftCorner, FrontRightCorner, BackLeftCorner, BackRightCorner);
                EstablishDetectionZones2(div, FrontLeftCorner, FrontRightCorner, BackLeftCorner, BackRightCorner, Front_Slope, Back_Slope, Left_Slope, Right_Slope);
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void EstablishDetectionZones2(Division div, Vector3 TopLeftCorner, Vector3 TopRightCorner, Vector3 BottomLeftCorner, Vector3 BottomRightCorner, float F_Slope, float B_Slope, float L_Slope, float R_Slope)
    {
        div.DetectionZones.Clear();

        float Front_Slope = ((float)Math.Round(TopRightCorner.z, 1) - (float)Math.Round(TopLeftCorner.z, 1)) / ((float)Math.Round(TopRightCorner.x, 1) - (float)Math.Round(TopLeftCorner.x, 1));
        float Back_Slope = ((float)Math.Round(BottomRightCorner.z, 1) - (float)Math.Round(BottomLeftCorner.z, 1)) / ((float)Math.Round(BottomRightCorner.x, 1) - (float)Math.Round(BottomLeftCorner.x, 1));
        float Left_Slope = ((float)Math.Round(TopLeftCorner.z, 1) - (float)Math.Round(BottomLeftCorner.z, 1)) / ((float)Math.Round(TopLeftCorner.x, 1) - (float)Math.Round(BottomLeftCorner.x, 1));
        float Right_Slope = ((float)Math.Round(TopRightCorner.z, 1) - (float)Math.Round(BottomRightCorner.z, 1)) / ((float)Math.Round(TopRightCorner.x, 1) - (float)Math.Round(BottomRightCorner.x, 1));


        float R = Mathf.Sqrt(1 + Mathf.Pow(Front_Slope, 2));
        float D = 3f;
        float FB_Polarity;
        float LR_Polarity;
        float LineAngle = CalculateLineAngle(TopLeftCorner, TopRightCorner);

        if (LineAngle > 90f && LineAngle < 270f)
            FB_Polarity = -1;
        else
            FB_Polarity = 1;

        Vector3 Right_Top = new Vector3(TopRightCorner.x + (FB_Polarity * D / R), 0f, TopRightCorner.z + ((FB_Polarity * D * Front_Slope) / R));
        Vector3 Left_Top = new Vector3(TopLeftCorner.x + (-FB_Polarity * D / R), 0f, TopLeftCorner.z + ((-FB_Polarity * D * Front_Slope) / R));

        R = Mathf.Sqrt(1 + Mathf.Pow(Back_Slope, 2));

        LineAngle = CalculateLineAngle(BottomLeftCorner, BottomRightCorner);

        if (LineAngle > 90f && LineAngle < 270f)
            FB_Polarity = -1;
        else
            FB_Polarity = 1;

        Vector3 Right_Bottom = new Vector3(BottomRightCorner.x + (FB_Polarity * D / R), 0f, BottomRightCorner.z + ((FB_Polarity * D * Back_Slope) / R));
        Vector3 Left_Bottom = new Vector3(BottomLeftCorner.x + (-FB_Polarity * D / R), 0f, BottomLeftCorner.z + ((-FB_Polarity * D * Back_Slope) / R));

        R = Mathf.Sqrt(1 + Mathf.Pow(Left_Slope, 2));

        LineAngle = CalculateLineAngle(TopLeftCorner, BottomLeftCorner);

        if (LineAngle > 90f && LineAngle < 270f)
            LR_Polarity = 1;
        else
            LR_Polarity = -1;

        Vector3 Top_Left = new Vector3(TopLeftCorner.x + (LR_Polarity * D / R), 0f, TopLeftCorner.z + ((LR_Polarity * D * Left_Slope) / R));
        Vector3 Bottom_Left = new Vector3(BottomLeftCorner.x + (-LR_Polarity * D / R), 0f, BottomLeftCorner.z + ((-LR_Polarity * D * Left_Slope) / R));

        R = Mathf.Sqrt(1 + Mathf.Pow(Right_Slope, 2));

        LineAngle = CalculateLineAngle(TopRightCorner, BottomRightCorner);

        if (LineAngle > 90f && LineAngle < 270f)
            LR_Polarity = 1;
        else
            LR_Polarity = -1f;

        Vector3 Top_Right = new Vector3(TopRightCorner.x + (LR_Polarity * D / R), 0f, TopRightCorner.z + ((LR_Polarity * D * Right_Slope) / R));
        Vector3 Bottom_Right = new Vector3(BottomRightCorner.x + (-LR_Polarity * D / R), 0f, BottomRightCorner.z + ((-LR_Polarity * D * Right_Slope) / R));

        if (double.IsNaN(Top_Left.z))
            Top_Left.z = TopLeftCorner.z + (FB_Polarity * 3);

        if (double.IsNaN(Bottom_Left.z))
            Bottom_Left.z = BottomLeftCorner.z - (FB_Polarity * 3);

        if (double.IsNaN(Top_Right.z))
            Top_Right.z = TopRightCorner.z + (FB_Polarity * 3);

        if (double.IsNaN(Bottom_Right.z))
            Bottom_Right.z = BottomRightCorner.z - (FB_Polarity * 3);

        if (double.IsNaN(Left_Top.x))
            Left_Top.x = TopLeftCorner.x + (LR_Polarity * 3);

        if (double.IsNaN(Left_Top.z))
            Left_Top.z = TopLeftCorner.z + (LR_Polarity * 3);

        if (double.IsNaN(Left_Bottom.z))
            Left_Bottom.z = BottomLeftCorner.z + (LR_Polarity * 3);

        if (double.IsNaN(Right_Top.z))
            Right_Top.z = TopRightCorner.z - (LR_Polarity * 3);

        if (double.IsNaN(Right_Bottom.z))
            Right_Bottom.z = BottomRightCorner.z - (LR_Polarity * 3);

        Vector3[] Vectors = new Vector3[] { Top_Left, Top_Right, TopLeftCorner, TopRightCorner };

        div.DetectionZones.Add(new DetectionZone(Direction.Forward, Vectors.ToList()));

        Vectors = new Vector3[] { Left_Top, Left_Bottom, TopLeftCorner, BottomLeftCorner };

        div.DetectionZones.Add(new DetectionZone(Direction.Left, Vectors.ToList()));

        Vectors = new Vector3[] { Right_Top, Right_Bottom, TopRightCorner, BottomRightCorner };

        div.DetectionZones.Add(new DetectionZone(Direction.Right, Vectors.ToList()));

        Vectors = new Vector3[] { Bottom_Left, Bottom_Right, BottomLeftCorner, BottomRightCorner };

        div.DetectionZones.Add(new DetectionZone(Direction.Back, Vectors.ToList()));

        DisplayDetectionZones(div);
    }

    public void DisplayDetectionZones(Division div)
    {
        try
        {
            Color[] colors = new Color[] { Color.white, Color.green, Color.green, Color.magenta };

            int x = 0;

            //Find the center of div
            Vector3 DivCenter = new Vector3();

            foreach (Vector3 Boundary in div.Boundaries)
                DivCenter = DivCenter + Boundary;

            DivCenter = DivCenter / 4;

            div.FlanksProtected.Clear();
            div.FlanksUnderAttack.Clear();

            List<Division> NearbyFriendlyDivisions = new List<Division>();
            List<Division> NearbyEnemyDivisions = new List<Division>();

            foreach (Division OtherDiv in DivisionList.Except(DivisionList.Where(D => D == div)))
            {
                Vector3 OtherDivCenter = new Vector3();

                foreach (Vector3 Boundary in OtherDiv.Boundaries)
                    OtherDivCenter = OtherDivCenter + Boundary;

                OtherDivCenter = OtherDivCenter / 4;

                float Distance = Vector3.Distance(DivCenter, OtherDivCenter);

                if (Distance < 25f)
                {
                    if (OtherDiv.side == div.side)
                        NearbyFriendlyDivisions.Add(OtherDiv);
                    else
                        NearbyEnemyDivisions.Add(OtherDiv);
                }
            }

            if (NearbyEnemyDivisions.Count > 0)
                div.NearbyEnemies = true;
            else
                div.NearbyEnemies = false;

            if (SwitchBoundaryDisplay)
            {
                foreach (DetectionZone zone in div.DetectionZones)
                {
                    Debug.DrawLine(zone.Corners[0], zone.Corners[1], colors[x]);
                    Debug.DrawLine(zone.Corners[0], zone.Corners[2], colors[x]);
                    Debug.DrawLine(zone.Corners[1], zone.Corners[3], colors[x]);
                    Debug.DrawLine(zone.Corners[2], zone.Corners[3], colors[x]);

                    x++;
                }
            }
            else
            {
                foreach (DetectionZone zone in div.DetectionZones)
                {
                    Debug.DrawLine(zone.Corners[2], zone.Corners[3], colors[x]);

                    x++;
                }
            }

            bool NotInCombat = true;

            foreach (Division EnemyDivision in NearbyEnemyDivisions)
            {
                List<Line> EnemyDivisionBoundaries = new List<Line>();

                EnemyDivisionBoundaries.Add(new Line("AB", TrimYAxis(EnemyDivision.Boundaries[0]), TrimYAxis(EnemyDivision.Boundaries[1])));
                EnemyDivisionBoundaries.Add(new Line("AC", TrimYAxis(EnemyDivision.Boundaries[0]), TrimYAxis(EnemyDivision.Boundaries[2])));
                EnemyDivisionBoundaries.Add(new Line("BD", TrimYAxis(EnemyDivision.Boundaries[1]), TrimYAxis(EnemyDivision.Boundaries[3])));
                EnemyDivisionBoundaries.Add(new Line("CD", TrimYAxis(EnemyDivision.Boundaries[2]), TrimYAxis(EnemyDivision.Boundaries[3])));

                foreach (DetectionZone zone in div.DetectionZones)
                {
                    List<Line> DivisionBoundaries = new List<Line>();

                    DivisionBoundaries.Add(new Line("AB", TrimYAxis(zone.Corners[0]), TrimYAxis(zone.Corners[1])));
                    DivisionBoundaries.Add(new Line("AC", TrimYAxis(zone.Corners[0]), TrimYAxis(zone.Corners[2])));
                    DivisionBoundaries.Add(new Line("BD", TrimYAxis(zone.Corners[1]), TrimYAxis(zone.Corners[3])));
                    DivisionBoundaries.Add(new Line("CD", TrimYAxis(zone.Corners[2]), TrimYAxis(zone.Corners[3])));

                    if (RectIntersect(DivisionBoundaries, EnemyDivisionBoundaries))
                    {
                        div.IsInCombat = true;
                        NotInCombat = false;
                        //Debug.Log("Enemy Division Detected by the " + zone.dir + " Flank");
                        div.FlanksUnderAttack.Add(zone.dir);
                    }
                }
            }

            if (NotInCombat)
                div.IsInCombat = false;

            foreach (Division FriendlyDivision in NearbyFriendlyDivisions)
            {
                List<Line> FriendlyDivisionBoundies = new List<Line>();

                FriendlyDivisionBoundies.Add(new Line("AB", TrimYAxis(FriendlyDivision.Boundaries[0]), TrimYAxis(FriendlyDivision.Boundaries[1])));
                FriendlyDivisionBoundies.Add(new Line("AC", TrimYAxis(FriendlyDivision.Boundaries[0]), TrimYAxis(FriendlyDivision.Boundaries[2])));
                FriendlyDivisionBoundies.Add(new Line("BD", TrimYAxis(FriendlyDivision.Boundaries[1]), TrimYAxis(FriendlyDivision.Boundaries[3])));
                FriendlyDivisionBoundies.Add(new Line("CD", TrimYAxis(FriendlyDivision.Boundaries[2]), TrimYAxis(FriendlyDivision.Boundaries[3])));

                foreach (DetectionZone zone in div.DetectionZones)
                {
                    List<Line> DivisionBoundaries = new List<Line>();

                    DivisionBoundaries.Add(new Line("AB", TrimYAxis(zone.Corners[0]), TrimYAxis(zone.Corners[1])));
                    DivisionBoundaries.Add(new Line("AC", TrimYAxis(zone.Corners[0]), TrimYAxis(zone.Corners[2])));
                    DivisionBoundaries.Add(new Line("BD", TrimYAxis(zone.Corners[1]), TrimYAxis(zone.Corners[3])));
                    DivisionBoundaries.Add(new Line("CD", TrimYAxis(zone.Corners[2]), TrimYAxis(zone.Corners[3])));

                    if (RectIntersect(DivisionBoundaries, FriendlyDivisionBoundies))
                    {
                        //Debug.Log("Friendly Division Detected by the " + zone.dir + " Flank");
                        div.FlanksProtected.Add(zone.dir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public bool RectIntersect(List<Line> Rect1, List<Line> Rect2)
    {
        try
        {
            foreach (Line line in Rect1)
            {
                foreach (Line line2 in Rect2)
                {
                    if (LinesIntersect(line.StartPoint, line.EndPoint, line2.StartPoint, line2.EndPoint))
                        return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
            return false;
        }
    }

    public float CalculateLineAngle(Vector3 A, Vector3 B)
    {
        try
        {
            float Angle = (float)Math.Atan2(Math.Round(B.z, 1) - Math.Round(A.z, 1), Math.Round(B.x, 1) - Math.Round(A.x, 1)) * Mathf.Rad2Deg;

            //Angle = (float)Math.Round(Angle);

            while (Angle > 360 || Angle < 0)
            {
                if (Angle > 360)
                    Angle -= 360;
                else if (Angle < 0)
                    Angle += 360;
            }

            return Angle;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
            return 0f;
        }
    }

    public bool FindDivisionOutlierNew(Vector3 A, Vector3 B, Vector3 C, Direction dir)
    {
        float AB_Angle = CalculateLineAngle(A, B);
        float AC_Angle = CalculateLineAngle(A, C);

        float result = AC_Angle - AB_Angle;

        result = Mathf.Round(result);

        if (dir == Direction.Left)
        {
            if (result < 0)
                result += 360;

            if (result > 180)
                return true;
            else
                return false;
        }

        if (dir == Direction.Right)
        {
            //New section was tacted but is mostly untested
            //It was tacted in response to a forward oriented left positioned test where the width was 6
            if (result > 0 || (result > -270 && result < -180))
                return true;
            else
                return false;
        }

        if (dir == Direction.Forward)
        {
            if (result < 0)
                result += 360;

            if (result <= 180 & result > 0)
                return true;
            else
                return false;
        }

        if (dir == Direction.Back)
        {
            //AB_Angle = (float)Math.Atan2(B.z - A.z, B.x - A.x) * Mathf.Rad2Deg;
            //float BA_Angle = (float)Math.Atan2(A.z - B.z, A.x - B.x) * Mathf.Rad2Deg;

            //AC_Angle = (float)Math.Atan2(C.z - A.z, C.x - A.x) * Mathf.Rad2Deg;
            float BA_Angle = CalculateLineAngle(B, A);

            //Comparision
            float AB_Angle2 = (float)Math.Atan2(B.z - A.z, B.x - A.x) * Mathf.Rad2Deg;
            float BA_Angle2 = (float)Math.Atan2(A.z - B.z, A.x - B.x) * Mathf.Rad2Deg;
            float AC_Angle2 = (float)Math.Atan2(C.z - A.z, C.x - A.x) * Mathf.Rad2Deg;

            //if (AB_Angle != AB_Angle2)
            //    Debug.Log("Altered: " + AB_Angle + "; Unaltered: " + AB_Angle2);

            //if (BA_Angle != BA_Angle2)
            //    Debug.Log("Altered: " + BA_Angle + "; Unaltered: " + BA_Angle2);

            //if (AC_Angle != AC_Angle2)
            //    Debug.Log("Altered: " + AC_Angle + "; Unaltered: " + AC_Angle2);

            //result = AC_Angle - AB_Angle;
            //float result2 = AC_Angle - BA_Angle;

            result = AC_Angle2 - AB_Angle2;
            float result2 = AC_Angle2 - BA_Angle2;

            //if ((result <= 0 && result2 > 0))
            //    return true;
            //else
            //    return false;
            if ((result < 0 && result2 > 0) || (result > 180 && result2 > 0) || (result < 0 && result2 < -180))
                return true;
            else
                return false;
        }

        return false;
    }

    public Soldier DistanceFromLineAB2(List<Soldier> SoldierList, Vector3 A, Vector3 B)
    {
        try
        {
            Soldier FurthestOutlier = SoldierList[0];
            float FurthestOutlierDistance = 0;

            foreach (Soldier sol in SoldierList)
            {
                Vector3 pos = sol.SoldierGO.transform.localPosition;

                float Dist = Vector3.Distance(pos, A) + Vector3.Distance(pos, B);

                if (Dist > FurthestOutlierDistance)
                {
                    FurthestOutlierDistance = Dist;
                    FurthestOutlier = sol;
                }
            }

            return FurthestOutlier;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
            return null;
        }
    }

    public void UpdateLogic()
    {
        try
        {
            foreach (Division div in DivisionList)
            {
                if (!div.IsInMotion && div.IsInCombat)
                {
                    Action act = () =>
                    {
                        SetCombatAnimations(div);
                    };

                    lock (FunctionsToRunInMainThread)
                        FunctionsToRunInMainThread.Add(act);
                }

                if (!div.IsInCombat && div.NearbyEnemies && div.SoldierList.Where(S => S.AnimState.Type != AnimationType.CombatIdle).ToList().Count > 0)
                {
                    if (div.IsInMotion)
                    {
                        //set animation to attack move
                    }
                    else
                    {
                        //Attack Ready
                        SetAnimations(AnimationType.CombatIdle, div.SoldierList);

                    }
                }
                else if (!div.NearbyEnemies && !div.IsInMotion && div.SoldierList.Where(S => S.AnimState.AnimationName != "Idle").ToList().Count > 0)
                {
                    //Idle
                    SetAnimations(AnimationType.Idle, div.SoldierList.Where(S => S.IsAlive).ToList());
                }

                if (div.IsInMotion)
                {
                    List<Soldier> SoldierList = div.SoldierList.Where(S => S.IsInMotion).ToList();

                    if (SoldierList.Count == 0)
                    {
                        div.IsInMotion = false;
                        break;
                    }

                    //Update Unit Pos Vector3
                    Action act = () =>
                    {
                        foreach (Soldier sol in SoldierList)
                            sol.CurrentPosition = sol.SoldierGO.transform.position;
                    };

                    lock (FunctionsToRunInMainThread)
                        FunctionsToRunInMainThread.Add(act);

                    //int AnimIndex = 0;
                    AnimationType type = AnimationType.Idle;

                    if (div.NearbyEnemies)
                    {
                        //Attack Ready
                        type = AnimationType.CombatIdle;
                    }

                    if (SoldierList.Count > 0)
                    {
                        List<Soldier> SoldiersToStop = new List<Soldier>();

                        for (int x = 0; x < SoldierList.Count; x++)
                        {
                            if (Mathf.Approximately((float)Math.Round(SoldierList[x].SoldierDestination.x, 2), (float)Math.Round(SoldierList[x].CurrentPosition.x, 2)) && Mathf.Approximately((float)Math.Round(SoldierList[x].SoldierDestination.z, 2), (float)Math.Round(SoldierList[x].CurrentPosition.z, 2)))
                            {
                                SoldiersToStop.Add(SoldierList[x]);
                            }
                        }

                        Action A = () =>
                        {
                            foreach (Soldier sol in SoldiersToStop.Where(S => S.IsAlive))
                            {
                                sol.IsInMotion = false;
                                sol.NavAgent.isStopped = true;

                                SetAnimations(type, new List<Soldier> { sol });
                                sol.SoldierGO.transform.rotation = div.DivisionGO.transform.rotation;
                            }
                        };

                        lock (FunctionsToRunInMainThread)
                            FunctionsToRunInMainThread.Add(A);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public Vector2 TrimYAxis(Vector3 Vec)
    {
        return new Vector2(Vec.x, Vec.z);
    }

    public void TimerLogic(Animation Anim, Soldier sol)
    {
        sol.AnimationTimer.Enabled = true;
        sol.AnimationTimer.Interval = Anim.Duration * 1000f;
        sol.AnimationTimer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) => AnimationElapsed(sol);
        sol.AnimationTimer.Start();

        SetAnimations(Anim.Type, new List<Soldier> { sol });
    }

    public void DeathTimerLogic(Soldier sol)
    {
        sol.IsAlive = false;

        sol.AnimationTimer.Enabled = true;
        sol.AnimationTimer.Interval = (sol.SubType.AnimationList.Where(A => A.Type == AnimationType.Death).ToList()[0].Duration * 1000f) - 500f;
        sol.AnimationTimer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) => DeathAnimationElapsed(sol);
        sol.AnimationTimer.Start();

        sol.SoldierGO.GetComponent<BoxCollider>().enabled = false;

        SetAnimations(AnimationType.Death, new List<Soldier> { sol });
    }

    public void AnimationElapsed(Soldier sol)
    {
        try
        {
            if (sol.IsInMotion)
                Debug.Log("Funny that");

            sol.AnimationTimer.Stop();
            sol.AnimationTimer.Enabled = false;
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void DeathAnimationElapsed(Soldier sol)
    {
        try
        {
            sol.AnimationTimer.Stop();
            sol.AnimationTimer.Enabled = false;

            Action act = () =>
            {
                MeshRenderer renderer = sol.SoldierGO.transform.Find(sol.rank.ToString() + " " + sol.state.ToString() + " " + sol.AnimState.AnimationName).gameObject.GetComponent<MeshRenderer>();
                renderer.material.SetFloat(renderer.material.shader.GetPropertyName(7), 2);
            };

            lock (FunctionsToRunInMainThread)
                FunctionsToRunInMainThread.Add(act);

            lock (FunctionsToRunInChildThread)
                FunctionsToRunInChildThread.Add(() => UpdatePositionInFormationAfterDeathOfSoldier(sol));

        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void UpdatePositionInFormationAfterDeathOfSoldier(Soldier DeadSoldier)
    {
        try
        {
            //If the division is engaged on one front
            if (DeadSoldier.AssignedDivision.FlanksUnderAttack.Count == 1)
            {
                switch (DeadSoldier.AssignedDivision.FlanksUnderAttack[0])
                {
                    case Direction.Forward:

                        Action act = () =>
                        {
                            List<Soldier> SoldiersToAdjust = DeadSoldier.AssignedDivision.SoldierList.Where(S => S.PositionInFormation.x == DeadSoldier.PositionInFormation.x && S.PositionInFormation.y > DeadSoldier.PositionInFormation.y && S.IsAlive).ToList();

                            DeadSoldier.NavAgent.enabled = false;

                            if (DeadSoldier.AssignedDivision.Destination == new Vector3())
                                Debug.Log("Destination is blah");

                            Vector3 NextSoldiersDestination = DeadSoldier.SoldierGO.transform.position;

                            SoldiersToAdjust = SoldiersToAdjust.OrderBy(S => S.PositionInFormation.y).ToList();

                            foreach (Soldier sol in SoldiersToAdjust)
                            {
                                sol.PositionInFormation = new Vector2(sol.PositionInFormation.x, sol.PositionInFormation.y - 1);

                                sol.SoldierDestination = NextSoldiersDestination;
                                sol.NavAgent.SetDestination(sol.SoldierDestination);

                                NextSoldiersDestination = sol.SoldierGO.transform.position;

                                if (sol.NavAgent.isStopped)
                                    sol.NavAgent.isStopped = false;
                            }

                            List<Soldier> RowOfSoldiers = DeadSoldier.AssignedDivision.SoldierList.Where(S => S.PositionInFormation.y == SoldiersToAdjust[SoldiersToAdjust.Count - 1].PositionInFormation.y + 1 && S.IsAlive).ToList();

                            if (RowOfSoldiers.Count == 0)
                                RowOfSoldiers = DeadSoldier.AssignedDivision.SoldierList.Where(S => S.PositionInFormation.y == SoldiersToAdjust[SoldiersToAdjust.Count - 1].PositionInFormation.y && S.IsAlive).ToList();

                            int RemainderYPos = (int)DeadSoldier.AssignedDivision.SoldierList.OrderByDescending(S => S.PositionInFormation.y).ToList()[0].PositionInFormation.y;

                            float X_Offset = 0f;

                            if (DeadSoldier.AssignedDivision.Width % 2 == 0)
                                X_Offset = -0.5f;

                            if (RowOfSoldiers.Count < DeadSoldier.AssignedDivision.Width && RemainderYPos != RowOfSoldiers[0].PositionInFormation.y)
                            {
                                if (RowOfSoldiers[0].PositionInFormation.y == 0)
                                    Debug.Log("Stop That");

                                List<Soldier> SoldiersToMove = new List<Soldier>();

                                int X_Sign = 1;

                                if (DeadSoldier.PositionInFormation.x > 0)
                                {
                                    SoldiersToMove = RowOfSoldiers.Where(S => S.PositionInFormation.x < DeadSoldier.PositionInFormation.x && S.PositionInFormation.x >= 0).OrderByDescending(S => S.PositionInFormation.x).ToList();
                                    X_Sign = -1;
                                }
                                else if (DeadSoldier.PositionInFormation.x < 0)
                                {
                                    SoldiersToMove = RowOfSoldiers.Where(S => S.PositionInFormation.x > DeadSoldier.PositionInFormation.x && S.PositionInFormation.x <= 0).OrderBy(S => S.PositionInFormation.x).ToList();
                                }

                                for (int x = 0; x < SoldiersToMove.Count; x++)
                                {
                                    //This probably shouldn't have the XOffset in there
                                    SoldiersToMove[x].PositionInFormation = new Vector2(DeadSoldier.PositionInFormation.x + (X_Sign * x), SoldiersToMove[x].PositionInFormation.y);

                                    SoldiersToMove[x].SoldierDestination = SoldiersToMove[x].AssignedDivision.DivisionGO.transform.TransformPoint(new Vector3(DeadSoldier.PositionInFormation.x + (X_Sign * x) + X_Offset, 0, -SoldiersToMove[x].PositionInFormation.y));

                                    SoldiersToMove[x].NavAgent.SetDestination(SoldiersToMove[x].SoldierDestination);

                                    if (SoldiersToMove[x].NavAgent.isStopped)
                                        SoldiersToMove[x].NavAgent.isStopped = false;
                                }
                            }

                            //Move the most central soldier in the remainder into the next row
                            if (DeadSoldier.AssignedDivision.SoldierList.Where(S => S.PositionInFormation == new Vector2(0, RemainderYPos - 1)).ToList().Count == 0)
                            {
                                Soldier MoveToLowerOrbital = DeadSoldier.AssignedDivision.SoldierList.Where(S => S.PositionInFormation.y == RemainderYPos).OrderBy(S => Mathf.Abs(S.PositionInFormation.x)).ToList()[0];

                                MoveToLowerOrbital.PositionInFormation = new Vector2(0, RemainderYPos - 1);

                                MoveToLowerOrbital.SoldierDestination = MoveToLowerOrbital.AssignedDivision.DivisionGO.transform.TransformPoint(new Vector3(X_Offset, 0f, -(RemainderYPos - 1)));

                                MoveToLowerOrbital.NavAgent.SetDestination(MoveToLowerOrbital.SoldierDestination);

                                if (MoveToLowerOrbital.NavAgent.isStopped)
                                    MoveToLowerOrbital.NavAgent.isStopped = false;
                            }

                            //Reorder the remaining soldiers in the remainder 
                            List<Soldier> Remainder = DeadSoldier.AssignedDivision.SoldierList.Where(S => S.PositionInFormation.y == RemainderYPos).OrderBy(S => S.PositionInFormation.x).ToList();

                            float RemainderXOffset = 0f;

                            if (Remainder.Count % 2 == 0)
                                RemainderXOffset = 0.5f;

                            for (int x = 0; x < Remainder.Count; x++)
                            {
                                Remainder[x].PositionInFormation = new Vector2(x + -(Remainder.Count / 2) + RemainderXOffset, RemainderYPos);

                                Remainder[x].SoldierDestination = Remainder[x].AssignedDivision.DivisionGO.transform.TransformPoint(new Vector3(x + -(Remainder.Count / 2) + RemainderXOffset, 0f, -RemainderYPos));

                                Remainder[x].NavAgent.SetDestination(Remainder[x].SoldierDestination);

                                if (Remainder[x].NavAgent.isStopped)
                                    Remainder[x].NavAgent.isStopped = false;
                            }
                        };

                        lock (FunctionsToRunInMainThread)
                            FunctionsToRunInMainThread.Add(act);

                        break;
                    case Direction.Back:

                        break;

                    case Direction.Left:

                        break;
                    case Direction.Right:

                        break;
                }
            }
            else
            {
                Debug.Log("Division is engaged on several fronts");
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    private void ToggleDivisionSelection(Division div, bool ToggleOn)
    {
        try
        {
            foreach (Soldier sol in div.SoldierList)
            {
                foreach (Transform child in sol.SoldierGO.transform)
                {
                    if (child.gameObject.name == "Cone")
                        continue;

                    MeshRenderer renderer = child.gameObject.GetComponent<MeshRenderer>();

                    if (renderer.material.shader.GetPropertyName(9) == "_IsSelected")
                    {
                        if (ToggleOn)
                            renderer.material.SetFloat(renderer.material.shader.GetPropertyName(9), 1);
                        else
                            renderer.material.SetFloat(renderer.material.shader.GetPropertyName(9), 0);
                    }
                    else
                        Debug.Log("Shader Property number nine is not _IsSelected");
                }
            }
        }
        catch(System.Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void RapidDeployment(Vector3 hit)
    {
        try
        {
            Vector3 Midpoint = new Vector3();
            Direction dir = Direction.LeftForward;
            float TotalWidth = 0f;

            foreach (Division div in SelectedDivisions)
            {
                TotalWidth += div.Width + 0.25f;
                Midpoint += div.DivisionGO.transform.position;
            }

            Midpoint = Midpoint / SelectedDivisions.Count;

            float AverageAngle = (Mathf.Atan2(hit.x - Midpoint.x, hit.z - Midpoint.z) * Mathf.Rad2Deg);

            //Debug.DrawLine(Midpoint, Midpoint + (Vector3.up * 4f), Color.magenta, 10f);

            while (AverageAngle >= 360 || AverageAngle < 0)
            {
                if (AverageAngle >= 360)
                    AverageAngle -= 360;
                if (AverageAngle < 0)
                    AverageAngle += 360;
            }

            Vector3 A = new Vector3(hit.x + (-(TotalWidth / 2) * Mathf.Cos(AverageAngle * (Mathf.PI / 180f))), 0f, hit.z - (-(TotalWidth / 2) * Mathf.Sin(AverageAngle * (Mathf.PI / 180f))));
            Vector3 B = new Vector3(hit.x - (-(TotalWidth / 2) * Mathf.Cos(AverageAngle * (Mathf.PI / 180f))), 0f, hit.z + (-(TotalWidth / 2) * Mathf.Sin(AverageAngle * (Mathf.PI / 180f))));

            Vector3 AB = A - B;

            Debug.DrawLine(hit, hit + (Vector3.up * 4f), Color.white, 50f);
            Debug.DrawLine(A, A + (Vector3.up * 4f), Color.blue, 50f);
            Debug.DrawLine(B, B + (Vector3.up * 4f), Color.red, 50f);

            Dictionary<float, Division> DivDistPairs = new Dictionary<float, Division>();
            List<Vector3> Destinations = new List<Vector3>();

            Vector3 StartPos = new Vector3(0f, 0.1f, 0f) + A - new Vector3(-(TotalWidth / (SelectedDivisions.Count * 2)) * Mathf.Cos(AverageAngle * (Mathf.PI / 180f)), 0f, (TotalWidth / (SelectedDivisions.Count * 2)) * Mathf.Sin(AverageAngle * (Mathf.PI / 180f)));

            for (int x = 0; x < SelectedDivisions.Count; x++)
                Destinations.Add(StartPos - (AB * ((float)x / (float)SelectedDivisions.Count)));

            foreach (Vector3 Dest in Destinations)
                Debug.DrawLine(Dest, Dest + (Vector3.up * 3f), Color.green, 50f);

            foreach (Division div in SelectedDivisions)
            {
                float CombinedDist = 0f;

                for (int i = 0; i < SelectedDivisions.Count; i++)
                    CombinedDist += Math.Abs(Vector3.Distance(div.DivisionGO.transform.position, Destinations[i]));

                DivDistPairs.Add(CombinedDist, div);
            }

            foreach (KeyValuePair<float, Division> Pair in DivDistPairs.OrderByDescending(D => D.Key))
            {
                float ClosestDist = 1000000000f;
                Vector3 ChosenDestination = new Vector3();

                //This method is currently "good enough" but it's pretty flawed
                foreach (Vector3 Dest in Destinations)
                {
                    float DistToDestination = Math.Abs(Vector3.Distance(Pair.Value.DivisionGO.transform.position, Dest));

                    if (DistToDestination < ClosestDist)
                    {
                        ClosestDist = DistToDestination;
                        ChosenDestination = Dest;
                    }
                }

                Destinations.Remove(ChosenDestination);

                float DivisionAngle = AverageAngle - Pair.Value.DivisionGO.transform.localEulerAngles.y;

                while (DivisionAngle >= 360 || DivisionAngle < 0)
                {
                    if (DivisionAngle >= 360)
                        DivisionAngle -= 360;
                    if (DivisionAngle < 0)
                        DivisionAngle += 360;
                }

                switch (DivisionAngle)
                {
                    case float a when a <= 45f || a >= 315f:
                        dir = Direction.Forward;
                        break;
                    case float a when a > 45f && a < 135f:
                        dir = Direction.Right;
                        break;
                    case float a when a >= 135f && a <= 225f:
                        dir = Direction.Back;
                        break;
                    case float a when a > 225f && a < 315f:
                        dir = Direction.Left;
                        break;
                }

                GameObject go = new GameObject();
                go.transform.position = ChosenDestination;
                go.transform.localEulerAngles = new Vector3(0f, AverageAngle, 0f);

                foreach (Soldier child in Pair.Value.SoldierList)
                    child.SoldierGO.transform.SetParent(go.transform);

                string strName = Pair.Value.DivisionGO.name;

                Destroy(Pair.Value.DivisionGO);
                go.name = strName;
                Pair.Value.DivisionGO = go;

                Pair.Value.TempWidth = Pair.Value.Width;

                Debug.Log("Division Angle: " + DivisionAngle);
                Debug.Log(dir.ToString());

                UpdateSoldierPositionsInFormationNew(Pair.Value, dir);

                Pair.Value.IsInMotion = true;

                foreach (Soldier sol in Pair.Value.SoldierList)
                    sol.IsInMotion = true;
            }
        }
        catch (Exception ex)
        {
            Debug.Log(ex.Message);
        }
    }

    public void UnitTest_DirectionStandardization(Direction InputDir, float LocalEulerAngleY)
    {
        Direction OutputDir = Direction.LeftForward;
        float AverageAngle = 0f;
        float LocalEulerAngle_Y_Rapid = 0f;

        Ray CursorRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        Ray StartPosRay = Camera.main.ScreenPointToRay(DivisionPlacementBoxStartPos);

        RaycastHit StartPoint;
        RaycastHit EndPoint;

        //They use binary instead of int for the layermask 
        // << is shifting a bit one spot to the left 
        Physics.Raycast(CursorRay, out EndPoint, 1200, 1 << 1);
        Physics.Raycast(StartPosRay, out StartPoint, 1200, 1 << 1);

        Vector3 MidPoint = (EndPoint.point + StartPoint.point) / 2;

        foreach (Division div in SelectedDivisions)
        {
            float Angle = (Mathf.Atan2(MidPoint.x - div.DivisionGO.transform.position.x, MidPoint.z - div.DivisionGO.transform.position.z) * Mathf.Rad2Deg) - div.DivisionGO.transform.localEulerAngles.y;

            while (Angle > 360 || Angle < 0)
            {
                if (Angle > 360)
                    Angle -= 360;
                else if (Angle < 0)
                    Angle += 360;
            }

            switch (Angle)
            {
                case float a when a <= 45f || a >= 315f:
                    Angle = 0;
                    break;
                case float a when a > 45f && a < 135f:
                    Angle = 90;
                    break;
                case float a when a >= 135f && a <= 225f:
                    Angle = 180;
                    break;
                case float a when a > 225f && a < 315f:
                    Angle = 270;
                    break;
            }

            AverageAngle += Angle;
        }

        AverageAngle = AverageAngle / SelectedDivisions.Count;

        while (AverageAngle > 360 || AverageAngle < 0)
        {
            if (AverageAngle > 360)
                AverageAngle -= 360;
            else if (AverageAngle < 0)
                AverageAngle += 360;
        }


        //Perhaps we should force the value here as well
        switch (AverageAngle)
        {
            case float a when a <= 45f || a >= 315f:
                OutputDir = Direction.Forward;
                break;
            case float a when a > 45f && a < 135f:
                OutputDir = Direction.Right;
                break;
            case float a when a >= 135f && a <= 225f:
                OutputDir = Direction.Back;
                break;
            case float a when a > 225f && a < 315f:
                OutputDir = Direction.Left;
                break;
        }

        float CurrentRotation = 0f;

        foreach (Division div in SelectedDivisions)
        {
            switch (div.DivisionGO.transform.localEulerAngles.y)
            {
                case float a when a <= 45f || a >= 315f:
                    CurrentRotation = 0f;
                    break;
                case float a when a > 45f && a < 135f:
                    CurrentRotation = 90f;
                    break;
                case float a when a >= 135f && a <= 225f:
                    CurrentRotation = 180f;
                    break;
                case float a when a > 225f && a < 315f:
                    CurrentRotation = 270f;
                    break;
            }

            LocalEulerAngle_Y_Rapid = AverageAngle + CurrentRotation;

            while (LocalEulerAngle_Y_Rapid > 360 || LocalEulerAngle_Y_Rapid < 0)
            {
                if (LocalEulerAngle_Y_Rapid > 360)
                    LocalEulerAngle_Y_Rapid -= 360;
                else if (LocalEulerAngle_Y_Rapid < 0)
                    LocalEulerAngle_Y_Rapid += 360;
            }

            Debug.Log(Environment.NewLine + "Rapid Direction: " + OutputDir.ToString() + Environment.NewLine + "Precise Direction: " + InputDir.ToString() + Environment.NewLine + "Rapid Angle: " + LocalEulerAngle_Y_Rapid + Environment.NewLine + "Precise Angle: " + LocalEulerAngleY + Environment.NewLine);

        }
    }

    void OnApplicationQuit()
    {
        DeathToChildThread = true;
    }
}
