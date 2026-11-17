using System;
using System.Collections.Generic;
namespace CastleFight {
// Fixed-step deadlines retain the exact float countdown convention, including
// its epsilon and overshoot. Variable dt rebuilds from the remaining countdown.
// Ready callbacks are returned in creation order, matching the original list.
public sealed class DeadlineQueue035<T> {
    sealed class Entry {public T item;public float remaining;public long due,sequence;public int startTick;}
    readonly List<Entry> heap=new List<Entry>(),ready=new List<Entry>();
    readonly List<T> result=new List<T>();
    readonly Action<T,float> write;readonly float epsilon;
    float step=-1;int tick;long serial;
    public DeadlineQueue035(Action<T,float> writer,float tolerance){write=writer;epsilon=tolerance;}
    static bool Before(Entry a,Entry b)=>a.due<b.due||a.due==b.due&&a.sequence<b.sequence;
    int Steps(float left,float dt){if(dt<=0)return left<=epsilon?1:int.MaxValue;int n=0;do{left-=dt;n++;}while(left>epsilon&&n<int.MaxValue);return n;}
    float Subtract(float left,float dt,int n){for(int i=0;i<n;i++)left-=dt;return left;}
    void Push(Entry e){int i=heap.Count;heap.Add(e);while(i>0){int p=(i-1)/2;if(!Before(e,heap[p]))break;heap[i]=heap[p];i=p;}heap[i]=e;}
    Entry Pop(){var root=heap[0];var tail=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);if(heap.Count==0)return root;int i=0;while(i*2+1<heap.Count){int c=i*2+1;if(c+1<heap.Count&&Before(heap[c+1],heap[c]))c++;if(!Before(heap[c],tail))break;heap[i]=heap[c];i=c;}heap[i]=tail;return root;}
    public void Add(T item,float left){var e=new Entry{item=item,remaining=left,sequence=serial++,startTick=tick};e.due=step<0?long.MaxValue:(long)tick+Steps(left,step);Push(e);}
    public List<T> Advance(float dt){
        if(step!=dt){
            ready.Clear();while(heap.Count>0)ready.Add(Pop());
            foreach(var e in ready){if(step>=0)e.remaining=Subtract(e.remaining,step,tick-e.startTick);e.startTick=tick;e.due=(long)tick+Steps(e.remaining,dt);Push(e);}step=dt;
        }
        tick++;ready.Clear();result.Clear();while(heap.Count>0&&heap[0].due<=tick)ready.Add(Pop());
        ready.Sort((a,b)=>a.sequence.CompareTo(b.sequence));
        foreach(var e in ready){write(e.item,Subtract(e.remaining,step,tick-e.startTick));result.Add(e.item);}return result;
    }
}
public sealed partial class HumanMatch {
    DeadlineQueue035<HumanJob> HumanQueue035(){var state=SimulationState035.Get(this);if(state.humanSchedule==null)state.humanSchedule=new DeadlineQueue035<HumanJob>((j,left)=>j.left=left,.00001f);return (DeadlineQueue035<HumanJob>)state.humanSchedule;}
    void QueueHumanJob035(HumanJob job){humanJobs.Add(job);if(!SimulationState035.Get(this).reference)HumanQueue035().Add(job,job.left);}
}
}
