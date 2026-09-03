using System;
using LitheEcs;

namespace ZeroAllocSurvival.Systems
{
    public interface ISystem
    {
    }

    public interface IInitializable
    {
        void Initialize();
    }

    public interface ITickable
    {
        void Tick(float deltaTime);
    }

    public abstract class BaseSystem : ISystem
    {
        protected World World { get; }
        protected EntityCommandBuffer CommandBuffer => World.CommandBuffer;

        protected BaseSystem(World world)
        {
            World = world;
        }
    }

    public abstract class QueryActionSystem : BaseSystem, IInitializable, ITickable
    {
        protected float DeltaTime { get; private set; }

        protected QueryActionSystem(World world) : base(world)
        {
        }

        void IInitializable.Initialize()
        {
            OnInitialize();
            OnPostInitialize();
        }

        void ITickable.Tick(float deltaTime)
        {
            DeltaTime = deltaTime;
            if (!OnPreTick()) return;
            OnTick();
            OnPostTick();
        }

        protected abstract void OnInitialize();

        protected virtual void OnPostInitialize()
        {
        }

        protected abstract void OnTick();

        protected virtual bool OnPreTick()
        {
            return true;
        }

        protected virtual void OnPostTick()
        {
        }
    }

    public abstract class QueryActionSystem<T1> : QueryActionSystem
        where T1 : struct
    {
        private readonly QueryAction<T1> _queryAction;
        private Query<T1> _query;

        protected QueryActionSystem(World world) : base(world)
        {
            _queryAction = ForEach;
        }

        protected sealed override void OnInitialize()
        {
            _query = CreateQuery().Warmup();
        }

        protected sealed override void OnTick()
        {
            _query.ForEach(_queryAction);
        }

        protected virtual Query<T1> CreateQuery() => World.Query<T1>();

        protected abstract void ForEach(in Entity entity, ref T1 t1);
    }

    public abstract class QueryActionSystem<T1, T2> : QueryActionSystem
        where T1 : struct
        where T2 : struct
    {
        private readonly QueryAction<T1, T2> _queryAction;
        private Query<T1, T2> _query;

        protected QueryActionSystem(World world) : base(world)
        {
            _queryAction = ForEach;
        }

        protected sealed override void OnInitialize()
        {
            _query = CreateQuery().Warmup();
        }

        protected sealed override void OnTick()
        {
            _query.ForEach(_queryAction);
        }

        protected virtual Query<T1, T2> CreateQuery() => World.Query<T1, T2>();

        protected abstract void ForEach(in Entity entity, ref T1 t1, ref T2 t2);
    }

    public abstract class QueryActionSystem<T1, T2, T3> : QueryActionSystem
        where T1 : struct
        where T2 : struct
        where T3 : struct
    {
        private readonly QueryAction<T1, T2, T3> _queryAction;
        private Query<T1, T2, T3> _query;

        protected QueryActionSystem(World world) : base(world)
        {
            _queryAction = ForEach;
        }

        protected sealed override void OnInitialize()
        {
            _query = CreateQuery().Warmup();
        }

        protected sealed override void OnTick()
        {
            _query.ForEach(_queryAction);
        }

        protected virtual Query<T1, T2, T3> CreateQuery() => World.Query<T1, T2, T3>();

        protected abstract void ForEach(in Entity entity, ref T1 t1, ref T2 t2, ref T3 t3);
    }

    public abstract class ParallelActionSystem<T1, T2, T3, T4, T5> : QueryActionSystem
        where T1 : struct
        where T2 : struct
        where T3 : struct
        where T4 : struct
        where T5 : struct
    {
        private readonly int _capacity;
        private readonly ParallelRangeAction<T1, T2, T3, T4, T5> _queryAction;
        private ParallelQuery<T1, T2, T3, T4, T5> _query;

        protected ParallelActionSystem(World world, int capacity) : base(world)
        {
            _capacity = capacity;
            _queryAction = ForEach;
        }

        protected sealed override void OnInitialize()
        {
            _query = CreateQuery().AsParallelQuery();
            _query.Reserve(_capacity);
        }

        protected sealed override void OnTick()
        {
            _query.Run(_queryAction);
        }

        protected virtual Query<T1, T2, T3, T4, T5> CreateQuery() => World.Query<T1, T2, T3, T4, T5>();

        protected abstract void ForEach(Span<T1> c1, Span<T2> c2, Span<T3> c3, Span<T4> c4, Span<T5> c5,
            EntityRange entities);
    }
}