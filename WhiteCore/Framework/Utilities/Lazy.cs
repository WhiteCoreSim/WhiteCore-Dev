/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org, http://opensimulator.org/
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

//
// Lazy.cs
//
// Authors:
//  Zoltan Varga (vargaz@gmail.com)
//  Marek Safar (marek.safar@gmail.com)
//
// Copyright (C) 2009 Novell
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;

namespace WhiteCore.Framework.Utilities
{
    public enum LazyThreadSafetyMode
    {
        None,
        PublicationOnly,
        ExecutionAndPublication
    }

    [Serializable]
    [ComVisible (false)]
    [HostProtection (SecurityAction.LinkDemand, Synchronization = true, ExternalThreading = true)]
    public class Lazy<T>
    {
        readonly LazyThreadSafetyMode mode;
        readonly object monitor;
        Exception exception;
        Func<T> factory;
        bool inited;
        T value;

        public Lazy ()
            : this (LazyThreadSafetyMode.ExecutionAndPublication)
        {
        }

        public Lazy (Func<T> valueFactory)
            : this (valueFactory, LazyThreadSafetyMode.ExecutionAndPublication)
        {
        }

        public Lazy (bool isThreadSafe)
            : this (
                Activator.CreateInstance<T>,
                isThreadSafe ? LazyThreadSafetyMode.ExecutionAndPublication : LazyThreadSafetyMode.None)
        {
        }

        public Lazy (Func<T> valueFactory, bool isThreadSafe)
            : this (valueFactory, isThreadSafe ? LazyThreadSafetyMode.ExecutionAndPublication : LazyThreadSafetyMode.None
                )
        {
        }

        public Lazy (LazyThreadSafetyMode mode)
            : this (Activator.CreateInstance<T>, mode)
        {
        }

        public Lazy (Func<T> valueFactory, LazyThreadSafetyMode mode)
        {
            if (valueFactory == null)
                throw new ArgumentNullException ("valueFactory");
            factory = valueFactory;
            if (mode != LazyThreadSafetyMode.None)
                monitor = new object ();
            this.mode = mode;
        }

        // Don't trigger expensive initialization
        [DebuggerBrowsable (DebuggerBrowsableState.Never)]
        public T Value {
            get {
                if (inited)
                    return value;
                if (exception != null)
                    throw exception;

                return InitValue ();
            }
        }

        public bool IsValueCreated {
            get { return inited; }
        }

        T InitValue ()
        {
            switch (mode) {
            case LazyThreadSafetyMode.None: {
                    var init_factory = factory;
                    if (init_factory == null)
                        throw exception =
                              new InvalidOperationException (
                                  "The initialization function tries to access Value on this instance");
                    try {
                        factory = null;
                        T v = init_factory ();
                        value = v;
                        Thread.MemoryBarrier ();
                        inited = true;
                    } catch (Exception ex) {
                        exception = ex;
                        throw;
                    }
                    break;
                }
            case LazyThreadSafetyMode.PublicationOnly: {
                    var init_factory = factory;
                    T v;

                    //exceptions are ignored
                    v = init_factory != null ? init_factory () : default (T);

                    lock (monitor) {
                        if (inited)
                            return value;
                        value = v;
                        Thread.MemoryBarrier ();
                        inited = true;
                        factory = null;
                    }
                    break;
                }
            case LazyThreadSafetyMode.ExecutionAndPublication: {
                    lock (monitor) {
                        if (inited)
                            return value;

                        if (factory == null)
                            throw exception =
                                  new InvalidOperationException (
                                      "The initialization function tries to access Value on this instance");

                        var init_factory = factory;
                        try {
                            factory = null;
                            T v = init_factory ();
                            value = v;
                            Thread.MemoryBarrier ();
                            inited = true;
                        } catch (Exception ex) {
                            exception = ex;
                            throw;
                        }
                    }
                    break;
                }
            default:
                throw new InvalidOperationException ("Invalid LazyThreadSafetyMode " + mode);
            }

            if (monitor == null) {
                //value = factory();
                value = default (T);        // 20160428 - greythane - not sure of this...  anyone?? :(
                lock (monitor)
                    inited = true;
            } else {
                lock (monitor) {
                    if (inited)
                        return value;

                    if (factory == null)
                        throw new InvalidOperationException (
                            "The initialization function tries to access Value on this instance");

                    var init_factory = factory;
                    try {
                        factory = null;
                        T v = init_factory ();
                        value = v;
                        Thread.MemoryBarrier ();
                        inited = true;
                    } catch {
                        factory = init_factory;
                        throw;
                    }
                }
            }

            return value;
        }

        public override string ToString ()
        {
            if (inited)
                return value.ToString ();
            return "Value is not created";
        }
    }
}
