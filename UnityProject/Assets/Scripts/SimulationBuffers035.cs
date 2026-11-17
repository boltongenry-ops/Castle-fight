using System;
using System.Collections;
using System.Collections.Generic;
namespace CastleFight {
// A lease owns its snapshot until Dispose. Nested damage/spawn callbacks get a
// different buffer: their snapshots must never overwrite the caller's iteration.
public static class SimulationBuffers035 {
    public static bool ReferenceAllocations;
    public static Snapshot<T> Take<T>(ICollection<T> source)=>new Snapshot<T>(source);
    public struct Snapshot<T>:IDisposable {
        static readonly Stack<T[]> pool=new Stack<T[]>();
        T[] data; readonly int count;
        internal Snapshot(ICollection<T> source){
            count=source.Count;data=null;
            if(!ReferenceAllocations&&pool.Count>0)data=pool.Pop();
            if(data==null||data.Length<count)data=new T[Math.Max(8,NextCapacity(count))];
            source.CopyTo(data,0);
        }
        static int NextCapacity(int n){int c=8;while(c<n)c*=2;return c;}
        public Enumerator GetEnumerator()=>new Enumerator(data,count);
        public void Dispose(){if(data==null)return;Array.Clear(data,0,count);if(!ReferenceAllocations&&pool.Count<8)pool.Push(data);data=null;}
        public struct Enumerator {
            readonly T[] data;readonly int count;int index;
            internal Enumerator(T[] a,int n){data=a;count=n;index=-1;}
            public bool MoveNext()=>++index<count;
            public T Current=>data[index];
        }
    }
}
}
