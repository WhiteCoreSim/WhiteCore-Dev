/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;

namespace WhiteCore.BotManager.AStar
{
    /// <summary>
    ///     Base class for path finding nodes, it holds no actual information about the map.
    ///     An inherited class must be constructed from this class and all virtual methods must be
    ///     implemented. Note, that calling base() in the overridden methods is not needed.
    /// </summary>
    public class AStarNode : IComparable
    {
        #region Properties

        double FGoalEstimate;
        AStarNode FGoalNode;

        /// <summary>
        ///     The parent of the node.
        /// </summary>
        public AStarNode Parent { get; set; }

        /// <summary>
        ///     The accumulative cost of the path until now.
        /// </summary>
        public double Cost { get; set; }

        /// <summary>
        ///     The estimated cost to the goal from here.
        /// </summary>
        public double GoalEstimate {
            set { FGoalEstimate = value; }
            get {
                Calculate();
                return (FGoalEstimate);
            }
        }

        /// <summary>
        ///     The cost plus the estimated cost to the goal from here.
        /// </summary>
        public double TotalCost {
            get { return (Cost + GoalEstimate); }
        }

        /// <summary>
        ///     The goal node.
        /// </summary>
        public AStarNode GoalNode {
            set {
                FGoalNode = value;
                Calculate();
            }
            get { return FGoalNode; }
        }

        #endregion

        #region Constructors

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="aParent">The node's parent</param>
        /// <param name="aGoalNode">The goal node</param>
        /// <param name="aCost">The accumulative cost until now</param>
        public AStarNode(AStarNode aParent, AStarNode aGoalNode, double aCost) {
            Parent = aParent;
            Cost = aCost;
            GoalNode = aGoalNode;
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Determines whether the current node is the goal.
        /// </summary>
        /// <returns>Returns true if current node is the goal</returns>
        public bool IsGoal() {
            return IsSameState(FGoalNode);
        }

        #endregion

        #region Virtual Methods

        /// <summary>
        ///     Determines whether the current node is the same state as the on passed.
        /// </summary>
        /// <param name="aNode">AStarNode to compare the current node to</param>
        /// <returns>Returns true if they are the same state</returns>
        public virtual bool IsSameState(AStarNode aNode) {
            return false;
        }

        /// <summary>
        ///     Calculates the estimated cost for the remaining trip to the goal.
        /// </summary>
        public virtual void Calculate() {
            FGoalEstimate = 0.0f;
        }

        /// <summary>
        ///     Gets all successors nodes from the current node and adds them to the successor list
        /// </summary>
        /// <param name="aSuccessors">List in which the successors will be added</param>
        public virtual void GetSuccessors(ArrayList aSuccessors) {
        }

        #endregion

        #region Overridden Methods

        public override bool Equals(object obj) {
            return IsSameState((AStarNode)obj);
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }

        #endregion

        #region IComparable Members

        public int CompareTo(object obj) {
            return (-TotalCost.CompareTo(((AStarNode)obj).TotalCost));
        }

        #endregion
    }

    /// <summary>
    ///     Class for performing A* path finding
    /// </summary>
    public sealed class AStar
    {
        #region Private Fields

        readonly Heap FClosedList;
        readonly Heap FOpenList;
        readonly ArrayList FSuccessors;
        AStarNode FGoalNode;
        AStarNode FStartNode;

        #endregion

        #region Properties

        readonly ArrayList FSolution;

        bool m_pathPossible = true;

        /// <summary>
        ///     Holds the solution after path finding is done. <see>FindPath()</see>
        /// </summary>
        public ArrayList Solution {
            get { return FSolution; }
        }

        public bool PathPossible {
            get { return m_pathPossible; }
        }

        #endregion

        #region Constructors

        public AStar() {
            FOpenList = new Heap();
            FClosedList = new Heap();
            FSuccessors = new ArrayList();
            FSolution = new ArrayList();
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Prints all the nodes in a list
        /// </summary>
        /// <param name="aNodeList">List to print</param>
        void PrintNodeList(object aNodeList) {
            Console.WriteLine("Node list:");
            foreach (AStarNode n in (aNodeList as IEnumerable)) {
                //n.PrintNodeInfo();
            }
            Console.WriteLine("=====");
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Finds the shortest path from the start node to the goal node
        /// </summary>
        /// <param name="aStartNode">Start node</param>
        /// <param name="aGoalNode">Goal node</param>
        public void FindPath(AStarNode aStartNode, AStarNode aGoalNode) {
            FStartNode = aStartNode;
            FGoalNode = aGoalNode;

            FOpenList.Add(FStartNode);
            int i = 0;
            while (FOpenList.Count > 0 && i < 2000) {
                // Get the node with the lowest TotalCost
                AStarNode NodeCurrent = (AStarNode)FOpenList.Pop();

                // If the node is the goal copy the path to the solution array
                if (NodeCurrent.IsGoal()) {
                    while (NodeCurrent != null) {
                        FSolution.Insert(0, NodeCurrent);
                        NodeCurrent = NodeCurrent.Parent;
                    }
                    break;
                }

                // Get successors to the current node
                NodeCurrent.GetSuccessors(FSuccessors);
                foreach (AStarNode NodeSuccessor in FSuccessors) {
                    // Test if the current successor node is on the open list, if it is and
                    // the TotalCost is higher, we will throw away the current successor.
                    AStarNode NodeOpen = null;
                    if (FOpenList.Contains(NodeSuccessor))
                        NodeOpen = (AStarNode)FOpenList[FOpenList.IndexOf(NodeSuccessor)];
                    if ((NodeOpen != null) && (NodeSuccessor.TotalCost > NodeOpen.TotalCost))
                        continue;

                    // Test if the current successor node is on the closed list, if it is and
                    // the TotalCost is higher, we will throw away the current successor.
                    AStarNode NodeClosed = null;
                    if (FClosedList.Contains(NodeSuccessor))
                        NodeClosed = (AStarNode)FClosedList[FClosedList.IndexOf(NodeSuccessor)];
                    if ((NodeClosed != null) && (NodeSuccessor.TotalCost > NodeClosed.TotalCost))
                        continue;

                    // Remove the old successor from the open list
                    FOpenList.Remove(NodeOpen);

                    // Remove the old successor from the closed list
                    FClosedList.Remove(NodeClosed);

                    // Add the current successor to the open list
                    FOpenList.Push(NodeSuccessor);
                }
                // Add the current node to the closed list
                FClosedList.Add(NodeCurrent);
                i++;
            }

            if (i == 2000) {
                m_pathPossible = false;
            }
        }

        #endregion
    }
}