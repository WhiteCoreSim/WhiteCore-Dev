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
    ///     The Heap allows to maintain a list sorted as long as needed.
    ///     If no IComparer interface has been provided at construction, then the list expects the Objects to implement IComparer.
    ///     If the list is not sorted it behaves like an ordinary list.
    ///     When sorted, the list's "Add" method will put new objects at the right place.
    ///     As well the "Contains" and "IndexOf" methods will perform a binary search.
    /// </summary>
    public class Heap : IList, ICloneable
    {
        #region Delegates

        /// <summary>
        ///     Defines an equality for two objects
        /// </summary>
        public delegate bool Equality(object object1, object object2);

        #endregion

        IComparer FComparer;
        ArrayList FList;
        bool FUseObjectsComparison;

        #region Constructors

        /// <summary>
        ///     Default constructor.
        ///     Since no IComparer is provided here, added objects must implement the IComparer interface.
        /// </summary>
        public Heap()
        {
            InitProperties(null, 0);
        }

        /// <summary>
        ///     Constructor.
        ///     Since no IComparer is provided, added objects must implement the IComparer interface.
        /// </summary>
        /// <param name="capacity">
        ///     Capacity of the list (<see cref="ArrayList.Capacity">ArrayList.Capacity</see>)
        /// </param>
        public Heap(int capacity)
        {
            InitProperties(null, capacity);
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="comparer">Will be used to compare added elements for sort and search operations.</param>
        public Heap(IComparer comparer)
        {
            InitProperties(comparer, 0);
        }

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="comparer">Will be used to compare added elements for sort and search operations.</param>
        /// <param name="capacity">
        ///     Capacity of the list (<see cref="ArrayList.Capacity">ArrayList.Capacity</see>)
        /// </param>
        public Heap(IComparer comparer, int capacity)
        {
            InitProperties(comparer, capacity);
        }

        #endregion

        #region Properties

        bool FAddDuplicates;

        /// <summary>
        ///     If set to true, it will not be possible to add an object to the list if its value is already in the list.
        /// </summary>
        public bool AddDuplicates
        {
            set { FAddDuplicates = value; }
            get { return FAddDuplicates; }
        }

        /// <summary>
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public int Capacity
        {
            get { return FList.Capacity; }
            set { FList.Capacity = value; }
        }

        #endregion

        #region ICloneable Members

        /// <summary>
        ///     ICloneable implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <returns>Cloned object.</returns>
        public object Clone()
        {
            Heap newClone = new Heap(FComparer, FList.Capacity)
                             {FList = (ArrayList) FList.Clone(), FAddDuplicates = FAddDuplicates};
            return newClone;
        }

        #endregion

        #region IList Members

        /// <summary>
        ///     IList implementation.
        ///     Gets object's value at a specified index.
        ///     The set operation is impossible on a Heap.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Index is less than zero or Index is greater than Count.</exception>
        /// <exception cref="InvalidOperationException">[] operator cannot be used to set a value on a Heap.</exception>
        public object this[int index]
        {
            get
            {
                if (index >= FList.Count || index < 0)
                    throw new ArgumentOutOfRangeException("Index is less than zero or Index is greater than Count.");
                return FList[index];
            }
            set { throw new InvalidOperationException("[] operator cannot be used to set a value in a Heap."); }
        }

        /// <summary>
        ///     IList implementation.
        ///     Adds the object at the right place.
        /// </summary>
        /// <param name="obj">The object to add.</param>
        /// <returns>The index where the object has been added.</returns>
        /// <exception cref="ArgumentException">The Heap is set to use object's IComparable interface, and the specified object does not implement this interface.</exception>
        public int Add(object obj)
        {
            int Return = -1;
            if (ObjectIsCompliant(obj))
            {
                int Index = IndexOf(obj);
                int NewIndex = Index >= 0 ? Index : -Index - 1;
                if (NewIndex >= Count) FList.Add(obj);
                else FList.Insert(NewIndex, obj);
                Return = NewIndex;
            }
            return Return;
        }

        /// <summary>
        ///     IList implementation.
        ///     Search for a specified object in the list.
        ///     If the list is sorted, a &lt;see cref = &quot;ArrayList.BinarySearch&quot;&gt;BinarySearch&lt;/see&gt; is performed using IComparer interface.
        ///     Else the &lt;see cref = &quot;Equals&quot;&gt;Object.Equals&lt;/see&gt; implementation is used.
        /// </summary>
        /// <param name="obj">The object to look for</param>
        /// <returns>true if the object is in the list, otherwise false.</returns>
        public bool Contains(object obj)
        {
            return FList.BinarySearch(obj, FComparer) >= 0;
        }

        /// <summary>
        ///     IList implementation.
        ///     Returns the index of the specified object in the list.
        ///     If the list is sorted, a &lt;see cref = &quot;ArrayList.BinarySearch&quot;&gt;BinarySearch&lt;/see&gt; is performed using IComparer interface.
        ///     Else the &lt;see cref = &quot;Equals&quot;&gt;Object.Equals&lt;/see&gt; implementation of objects is used.
        /// </summary>
        /// <param name="obj">The object to locate.</param>
        /// <returns>
        ///     If the object has been found, a positive integer corresponding to its position.
        ///     If the objects has not been found, a negative integer which is the bitwise complement of the index of the next element.
        /// </returns>
        public int IndexOf(object obj)
        {
            int Result = -1;
            Result = FList.BinarySearch(obj, FComparer);
            while (Result > 0 && FList[Result - 1].Equals(obj))
                Result--;
            return Result;
        }

        /// <summary>
        ///     IList implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public bool IsFixedSize
        {
            get { return FList.IsFixedSize; }
        }

        /// <summary>
        ///     IList implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public bool IsReadOnly
        {
            get { return FList.IsReadOnly; }
        }

        /// <summary>
        ///     IList implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public void Clear()
        {
            FList.Clear();
        }

        /// <summary>
        ///     IList implementation.
        ///     Cannot be used on a Heap.
        /// </summary>
        /// <param name="index">The index before which the object must be added.</param>
        /// <param name="obj">The object to add.</param>
        /// <exception cref="InvalidOperationException">Insert method cannot be called on a Heap.</exception>
        public void Insert(int index, object obj)
        {
            throw new InvalidOperationException("Insert method cannot be called on a Heap.");
        }

        /// <summary>
        ///     IList implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <param name="value">The object whose value must be removed if found in the list.</param>
        public void Remove(object value)
        {
            FList.Remove(value);
        }

        /// <summary>
        ///     IList implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <param name="index">Index of object to remove.</param>
        public void RemoveAt(int index)
        {
            FList.RemoveAt(index);
        }

        /// <summary>
        ///     IList.ICollection implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(Array array, int arrayIndex)
        {
            FList.CopyTo(array, arrayIndex);
        }

        /// <summary>
        ///     IList.ICollection implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public int Count
        {
            get { return FList.Count; }
        }

        /// <summary>
        ///     IList.ICollection implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public bool IsSynchronized
        {
            get { return FList.IsSynchronized; }
        }

        /// <summary>
        ///     IList.ICollection implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        public object SyncRoot
        {
            get { return FList.SyncRoot; }
        }

        /// <summary>
        ///     IList.IEnumerable implementation.
        ///     Idem <see cref="ArrayList">ArrayList</see>
        /// </summary>
        /// <returns>Enumerator on the list.</returns>
        public IEnumerator GetEnumerator()
        {
            return FList.GetEnumerator();
        }

        #endregion

        /// <summary>
        ///     Object.ToString() override.
        ///     Build a string to represent the list.
        /// </summary>
        /// <returns>The string reflecting the list.</returns>
        public override string ToString()
        {
            string OutString = "{";
            for (int i = 0; i < FList.Count; i++)
                OutString += FList[i] + (i != FList.Count - 1 ? "; " : "}");
            return OutString;
        }

        /// <summary>
        ///     Object.Equals() override.
        /// </summary>
        /// <returns>true if object is equal to this, otherwise false.</returns>
        public override bool Equals(object obj)
        {
            Heap SL = (Heap) obj;
            if (SL.Count != Count)
                return false;
            for (int i = 0; i < Count; i++)
                if (!SL[i].Equals(this[i]))
                    return false;
            return true;
        }

        /// <summary>
        ///     Object.GetHashCode() override.
        /// </summary>
        /// <returns>Hash code for this.</returns>
        public override int GetHashCode()
        {
            return FList.GetHashCode();
        }

        /// <summary>
        ///     Idem IndexOf(object), but starting at a specified position in the list
        /// </summary>
        /// <param name="obj">The object to locate.</param>
        /// <param name="start">The index for start position.</param>
        /// <returns></returns>
        public int IndexOf(object obj, int start)
        {
            int Result = -1;
            Result = FList.BinarySearch(start, FList.Count - start, obj, FComparer);
            while (Result > start && FList[Result - 1].Equals(obj))
                Result--;
            return Result;
        }

        /// <summary>
        ///     Idem IndexOf(object), but with a specified equality function
        /// </summary>
        /// <param name="obj">The object to locate.</param>
        /// <param name="areEqual">Equality function to use for the search.</param>
        /// <returns></returns>
        public int IndexOf(object obj, Equality areEqual)
        {
            for (int i = 0; i < FList.Count; i++)
                if (areEqual(FList[i], obj)) return i;
            return -1;
        }

        /// <summary>
        ///     Idem IndexOf(object), but with a start index and a specified equality function
        /// </summary>
        /// <param name="obj">The object to locate.</param>
        /// <param name="start">The index for start position.</param>
        /// <param name="areEqual">Equality function to use for the search.</param>
        /// <returns></returns>
        public int IndexOf(object obj, int start, Equality areEqual)
        {
            if (start < 0 || start >= FList.Count)
                throw new ArgumentException("Start index must belong to [0; Count-1].");
            for (int i = start; i < FList.Count; i++)
                if (areEqual(FList[i], obj)) return i;
            return -1;
        }

        /// <summary>
        ///     The objects will be added at the right place.
        /// </summary>
        /// <param name="coll">The object to add.</param>
        /// <returns>The index where the object has been added.</returns>
        /// <exception cref="ArgumentException">The Heap is set to use object's IComparable interface, and the specified object does not implement this interface.</exception>
        public void AddRange(ICollection coll)
        {
            foreach (object Object in coll)
                Add(Object);
        }

        /// <summary>
        ///     Cannot be called on a Heap.
        /// </summary>
        /// <param name="index">The index before which the objects must be added.</param>
        /// <param name="coll">The object to add.</param>
        /// <exception cref="InvalidOperationException">Insert cannot be called on a Heap.</exception>
        public void InsertRange(int index, ICollection coll)
        {
            throw new InvalidOperationException("Insert cannot be called on a Heap.");
        }

        /// <summary>
        ///     Limits the number of occurrences of a specified value.
        ///     Same values are equals according to the Equals() method of objects in the list.
        ///     The first occurrences encountered are kept.
        /// </summary>
        /// <param name="value">Value whose occurrences number must be limited.</param>
        /// <param name="numberToKeep">Number of occurrences to keep</param>
        public void LimitOccurrences(object value, int numberToKeep)
        {
            if (value == null)
                throw new ArgumentNullException("Value is null");
            int Pos = 0;
            while ((Pos = IndexOf(value, Pos)) >= 0)
            {
                if (numberToKeep <= 0)
                    FList.RemoveAt(Pos);
                else
                {
                    Pos++;
                    numberToKeep--;
                }
                if (FComparer.Compare(FList[Pos], value) > 0)
                    break;
            }
        }

        /// <summary>
        ///     Removes all duplicates in the list.
        ///     Each value encountered will have only one representant
        /// </summary>
        public void RemoveDuplicates()
        {
            int PosIt;
            PosIt = 0;
            while (PosIt < Count - 1)
            {
                if (FComparer.Compare(this[PosIt], this[PosIt + 1]) == 0)
                    RemoveAt(PosIt);
                else
                    PosIt++;
            }
        }

        /// <summary>
        ///     Returns the object of the list whose value is minimum
        /// </summary>
        /// <returns>The minimum object in the list</returns>
        public int IndexOfMin()
        {
            int RetInt = -1;
            if (FList.Count > 0)
                RetInt = 0;
            return RetInt;
        }

        /// <summary>
        ///     Returns the object of the list whose value is maximum
        /// </summary>
        /// <returns>The maximum object in the list</returns>
        public int IndexOfMax()
        {
            int RetInt = -1;
            if (FList.Count > 0)
            {
                RetInt = FList.Count - 1;
            }
            return RetInt;
        }

        /// <summary>
        ///     Returns the topmost object on the list and removes it from the list
        /// </summary>
        /// <returns>Returns the topmost object on the list</returns>
        public object Pop()
        {
            if (FList.Count == 0)
                throw new InvalidOperationException("The heap is empty.");
            object Object = FList[Count - 1];
            FList.RemoveAt(Count - 1);
            return (Object);
        }

        /// <summary>
        ///     Pushes an object on list. It will be inserted at the right spot.
        /// </summary>
        /// <param name="obj">Object to add to the list</param>
        /// <returns></returns>
        public int Push(object obj)
        {
            return (Add(obj));
        }

        bool ObjectIsCompliant(object obj)
        {
            if (FUseObjectsComparison && !(obj is IComparable))
                throw new ArgumentException(
                    "The Heap is set to use the IComparable interface of objects, and the object to add does not implement the IComparable interface.");
            if (!FAddDuplicates && Contains(obj))
                return false;
            return true;
        }

        void InitProperties(IComparer comparer, int capacity)
        {
            if (comparer != null)
            {
                FComparer = comparer;
                FUseObjectsComparison = false;
            }
            else
            {
                FComparer = new Comparison();
                FUseObjectsComparison = true;
            }
            FList = capacity > 0 ? new ArrayList(capacity) : new ArrayList();
            FAddDuplicates = true;
        }

        #region Nested type: Comparison

        class Comparison : IComparer
        {
            #region IComparer Members

            public int Compare(object obj1, object obj2)
            {
                IComparable C = obj1 as IComparable;
                return C.CompareTo(obj2);
            }

            #endregion
        }

        #endregion
    }
}