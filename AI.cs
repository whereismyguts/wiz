﻿using System;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk {
    public enum State { InBattle, LookFor };
    public enum Problem { Run, Attack, Push, Defend , Bonus};
    internal class AI {
        static Wizard Me; static World World; static Game Game; static Move Move;
        static Grid grid;
        static Vector moveTarget;
        static UnitInfo attackTarget;
        static bool inBattle = false;
        static Problem problem;

        static List<UnitInfo> AllLivingUnits = new List<UnitInfo>(); // must be ordered
        static List<UnitInfo> EnemyUnitsInFight = new List<UnitInfo>(); // must be ordered

        internal static void MakeMove(Wizard me, World world, Game game, Move move) {
            InitializeTick(me, world, game, move);
            GatherInfo();
            inBattle = InBattle(); //1st level
            problem = CalcProblem(); //2nd level
            CalcTargets(); //3rd level
            ProcessTargets(); //4th level
        }

        static void ProcessTargets() {
            if(attackTarget != null) {
                SmartAttack(attackTarget);
                SmartWalk(moveTarget);
            }
            else {
                GoSimple(moveTarget);
            }
        }
        static void SmartAttack(UnitInfo attackTarget) {
            if(CanShoot()) {
                Move.MinCastDistance = attackTarget.Distance - attackTarget.Unit.Radius * 1.1;
                Move.MaxCastDistance = attackTarget.Distance + attackTarget.Unit.Radius * 1.1;
                Kick(attackTarget, ActionType.MagicMissile);
            }
            else {
                var near = EnemyUnitsInFight.FirstOrDefault(e => e.Distance <= Me.Radius * 1.5);
                if(near != null) {
                    Kick(near, ActionType.Staff);
                }
                else
                    Kick(attackTarget, ActionType.None);
            }
        }

        private static void Kick(UnitInfo target, ActionType type) {
            double angle = Me.GetAngleTo(target.Unit);
            if(Math.Abs(angle) <= 0.01)
                Move.Action = type;
            else
                Move.Turn = angle;
        }

        static bool CanShoot() {
            return Me.RemainingActionCooldownTicks < 5 && Me.RemainingCooldownTicksByAction[(int)ActionType.MagicMissile] < 5;
        }
        static void SmartWalk(Vector goal) {
            if(goal.IsEmpty)
                return;
            Vector meToGoal = goal - new Vector(Me.X, Me.Y);
            Vector correctSpeed = meToGoal.SetLength(3.0);
            Vector correctDir = correctSpeed.Rotate(-Me.Angle);

            Move.Speed = correctDir.X;
            Move.StrafeSpeed = correctDir.Y;
            //Move.Turn = Me.GetAngleTo(goal.X, goal.Y);
        }
        static void GoSimple(Vector goal) {
            if(grid != null) {
                var path = grid.GetPath(Me.X, Me.Y, moveTarget.X, moveTarget.Y);
                if(path != null && path.Count > 1) {
                    GoStupid(path[1].X, path[1].Y);
                    return;
                }
            }
            GoStupid(goal.X, goal.Y);
        }
        static void GoStupid(double x, double y) {
            if(WalkAround())
                return;
            Move.Turn = Me.GetAngleTo(x, y);
            Move.Speed = Game.WizardForwardSpeed;
        }
        static bool WalkAround() {
            try {
                UnitInfo obj = AllLivingUnits.Where(b => b.Unit.Id != Me.Id).FirstOrDefault(); // must be ordered 
                double minDist = Me.Radius + obj.Unit.Radius + 30;
                double angle = Me.GetAngleTo(obj.Unit.X, obj.Unit.Y);

                if(Math.Abs(angle) <= Math.PI && obj.Distance <= minDist) {

                    if(walkAroundcounter == 30)
                        walkArounddir = -2;
                    if(walkAroundcounter == 0)
                        walkArounddir = 1;
                    Move.Speed = Game.WizardForwardSpeed;

                    Move.Turn = -angle;
                    return true;
                }
            }
            catch(Exception e) {

            };
            return false;
        }
        static int walkAroundcounter = 0;
        static int walkArounddir = 1;
        static void CalcTargets() {
            moveTarget = CalcMoveTarget();
            attackTarget = CalcAttackTarget();
        }
        static UnitInfo CalcAttackTarget() {
            if(EnemyUnitsInFight.Count > 0)
                return EnemyUnitsInFight.FirstOrDefault(); // mist be ordered
            var tree = AllLivingUnits.Find(u => u.Unit is Tree && u.Distance <= Me.Radius * 1.5);// must be ordered
            return tree;
        }
        static Vector CalcMoveTarget() {
            switch(problem) {
                case Problem.Attack:
                    return CalcOptimalLocalPoint(false);
                case Problem.Run:
                    return CalcOptimalLocalPoint(true);
                case Problem.Bonus:
                    return bonus;
                case Problem.Push:
                    return CalcLanePoint();
                case Problem.Defend:
                    return UnitInfo.HomeBase;
            }
            return new Vector(2000, 2000);

        }
        static Vector CalcLanePoint() {

            var en = AllLivingUnits.Where(u => u.IsEnemy).FirstOrDefault();

            return en == null ? new Vector(2000, 2000) : new Vector(en.Unit.X, en.Unit.Y);

        }
        static Vector CalcOptimalLocalPoint(bool run) {
            // return new Vector();
            var blocks = AllLivingUnits.Where(u => u.Distance < Me.CastRange).ToList(); // must be order


            Vector result = new Vector(); ;
            double bestValue = 0;

            //double rMax = Me.CastRange/2 ;
            double rMin = Me.Radius;
            double rStep = Me.Radius;


            double R = rMin + rStep;
            List<double> dotvalues = new List<double>();
            List<Vector> dots = new List<Vector>();
            for(double fi = 0; fi < Math.PI * 2; fi += Math.PI / 24) {
                Vector dot = new Vector(
                    Me.X + R * Math.Cos(fi),
                    Me.Y + R * Math.Sin(fi));
                if(dot.X < 50 || dot.X > 3950 || dot.Y < 50 || dot.Y > 3950)
                    continue;

                double dAverage = blocks.Sum(u => u.Unit.GetDistanceTo(dot.X, dot.Y)) / blocks.Count;

                var values = blocks.Select(b => b.DotValueInFight(dot, run, dAverage)).ToList();
                double value = values.Sum() / values.Count;
                dotvalues.Add(value);
                dots.Add(dot);
                if(value > bestValue) {
                    result = dot;
                    bestValue = value;
                }
            }
            //if(!result.IsEmpty)
            //    DrawOptimalPoint(result, blocks, dots, dotvalues);

            return result.IsEmpty ? UnitInfo.HomeBase : result;
        }

        private static void DrawOptimalPoint(Vector result, List<UnitInfo> blocks, List<Vector> dots, List<double> vals) {
            //  Point zero = new Point((int)Me.X, (int)Me.Y);
            float maxx = (float)(blocks.OrderBy(b => b.Unit.X).Last().Unit.X);
            float minx = (float)(blocks.OrderBy(b => b.Unit.X).First().Unit.X);
            float miny = (float)(blocks.OrderBy(b => b.Unit.Y).First().Unit.Y);
            float maxy = (float)(blocks.OrderBy(b => b.Unit.Y).Last().Unit.Y);


            Bitmap bmp = new Bitmap((int)(maxx - minx) + 100, (int)(maxy - miny) + 100);
            Graphics gr = Graphics.FromImage(bmp);

            blocks.Add(new UnitInfo(Me));

            foreach(var bl in blocks) {
                gr.DrawEllipse(bl.IsEnemy ? Pens.Red : bl.Unit.Id == Me.Id ? Pens.Green : bl.Unit.Faction == Faction.Other ? Pens.GreenYellow : Pens.Gray,
                    (float)bl.Unit.X - minx- (float)bl.Unit.Radius, (float)bl.Unit.Y - miny- (float)bl.Unit.Radius, (float)bl.Unit.Radius*2, (float)bl.Unit.Radius*2);
            }

            var averageVal = vals.Sum() / vals.Count;

            for(int i = 0; i < dots.Count; i++) {

                Point p = dots[i].toPoint() - new Point((int)minx, (int)miny);
           

                gr.FillEllipse(vals[i] > averageVal ? Brushes.Green : vals[i]< averageVal ? Brushes.Red : Brushes.Orange, p.X , p.Y , 10,10);
            }

            gr.DrawEllipse(Pens.White, (float)result.X - minx , (float)result.Y - miny , 10, 10 );
            bmp.Save("local.png", ImageFormat.Png);

        }
        static Vector bonus;
        static Problem CalcProblem() {
            if(inBattle) {
                return Me.Life < Me.MaxLife * 0.5 || DangerPlace() ? Problem.Run : Problem.Attack;
            }
            if(World.Bonuses.Count()>0) {
                bonus = new Vector(World.Bonuses[0].X, World.Bonuses[0].Y);
                return Problem.Bonus;
            }

            return Problem.Push; // TODo add defend
        }
        static bool DangerPlace() {
            var friendlyUnitsNear = AllLivingUnits.Where(
                u => u.Unit.Faction == Me.Faction &&
                u.Distance < Me.VisionRange / 0.7
            ).ToList();
            return friendlyUnitsNear == null || EnemyUnitsInFight.Count - friendlyUnitsNear.Count > 3;
        }
        static bool InBattle() {
            return EnemyUnitsInFight.Count > 0;
        }
        static void InitializeTick(Wizard me, World world, Game game, Move move) {
            Me = me;
            World = world;
            Game = game;
            Move = move;
        }
        static void GatherInfo() {
            UnitInfo.Me = Me; UnitInfo.SetParams();
            UnitInfo.Game = Game;

            UpdateMap();
            List<LivingUnit> objects = new List<LivingUnit>(World.Wizards);
            objects.AddRange(World.Minions);
            objects.AddRange(World.Trees);
            objects.AddRange(World.Buildings);

            AllLivingUnits = objects.Where(u => u.Id != Me.Id).Select(o => new UnitInfo(o)).OrderBy(u => u.Distance).ToList();
            EnemyUnitsInFight = AllLivingUnits.Where(u => u.IsEnemy && u.Distance <= Me.VisionRange).OrderBy(u => u.Distance).ToList();
        }
        static void UpdateMap() {
            var objects = new List<CircularUnit>();
            objects.AddRange(World.Buildings);
            objects.AddRange(World.Trees);
            if(grid == null)
                grid = new Grid(objects);
            else
                grid.Reveal(objects);
        }
    }
    class UnitInfo {
        public static Wizard Me { get; set; }
        public static Game Game { get; set; }
        public LivingUnit Unit { get; internal set; }
        public double Distance { get; internal set; }
        public static bool ShouldInit { get; set; }
        public static bool AttackNeutrals { get; set; } = false;
        public static Vector HomeBase { get; set; }
        public static Vector TheirBase { get; set; }
        public static Faction They { get; set; }
        public bool IsEnemy {
            get {
                return Unit.Faction == They ||
                  (Unit.Faction == Faction.Neutral &&
                      (AttackNeutrals || Unit.Life < Unit.MaxLife));
            }
        }

        public UnitInfo(LivingUnit unit) {
            Unit = unit;
            Distance = Unit.GetDistanceTo(Me);
        }

        internal static void SetParams() {
            HomeBase = Me.Faction == Faction.Academy ? new Vector(600, 3390) : new Vector(3390, 600);
            TheirBase = Me.Faction == Faction.Renegades ? new Vector(600, 3390) : new Vector(3390, 600);
            They = Me.Faction == Faction.Academy ? Faction.Renegades : Faction.Academy;
        }
        internal double DotValueInFight(Vector dot) {
            //TODo: include ray!

            return 1000 / Unit.GetDistanceTo(dot.X, dot.Y) + (IsEnemy ? -1 : Distance);
        }
        public override string ToString() {
            return Unit.Faction.ToString() + " " + Unit.GetType().Name + ", d:" + Distance;
        }


        internal double DotValueInFight(Vector dot, bool run, double dAverage) {
            double unitDist = dot.DistanceTo(Unit.X, Unit.Y);
            double vAvoid = IsEnemy ? 0 :
               dAverage/unitDist;
            double vSafe = 1;
            if(IsEnemy) {
                if(run)
                    vSafe = unitDist > GetCastRange() ? 1 : 0;

                else vSafe = unitDist > Me.CastRange * 0.7 && unitDist < Me.CastRange*0.9 ? 1 : 0;
            }

            double vAccess = unitDist <= Unit.Radius + 1.5 * Me.Radius ? 0 : 1;

            return vAvoid * vAccess * vSafe;
        }

        private double GetCastRange() {
            if(Unit is Minion) 
                return Game.FetishBlowdartAttackRange;
            if(Unit is Wizard)
                return ((Wizard)Unit).CastRange;
            if(Unit is Building)
                return ((Building)Unit).AttackRange;
            return Me.CastRange;
        }

        private bool IsTripleRayInterSectsSomeone(Vector dot) {
            double deg90 = Math.PI / 2;
            Vector me = new Vector(Me.X, Me.Y);

            Vector Ray = dot - me;
            Vector Lm = Ray.Rotate(deg90).SetLength(Me.Radius * 1.5);
            Vector Rm = Ray.Rotate(-deg90).SetLength(Me.Radius * 1.5);

            Vector yar = me - dot;
            Vector Ld = Ray.Rotate(-deg90).SetLength(Me.Radius * 1.5);
            Vector Rd = Ray.Rotate(deg90).SetLength(Me.Radius * 1.5);

            Vector RayL = Ld - Lm;
            Vector RayR = Rd - Rm;

          

            return IsRayIntersectsCircle(me, me + Ray, new Vector( Unit.X,Unit.Y),Me.Radius * 2) ||
                IsRayIntersectsCircle(me + Lm, me + RayL, new Vector(Unit.X, Unit.Y), Me.Radius * 2) ||
                IsRayIntersectsCircle(me + Rm, me + RayR, new Vector(Unit.X, Unit.Y), Me.Radius * 2);

        }

        private bool IsRayIntersectsCircle(Vector v0, Vector v1, Vector center, double r) {
            double alfa = Vector.Angle((center - v0), (v1 - v0));
            double distFromCenterToRay = (center - v0).DistanceTo(0, 0); // length
            return r >= Math.Abs( distFromCenterToRay * Math.Cos(alfa));
        }
    }
}