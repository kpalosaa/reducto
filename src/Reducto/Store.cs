using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Reducto
{
    public sealed class InitStoreAction
    {
    }

    public delegate State Reducer<State>(State state, Object action);
    public delegate void StateChangedSubscriber<State>(State state);
    public delegate void Unsubscribe();
    public delegate void DispatcherDelegate(Object a);

    public interface IBasicStore<State>
    {
        Unsubscribe Subscribe(StateChangedSubscriber<State> subscription);
        void Dispatch(Object action);
        State GetState();
    }

	public interface IDispatcher<State>
	{
		void Dispatch(Object action);
		Task<Result> Dispatch<Result>(Store<State>.AsyncAction<Result> action);
		Task Dispatch(Store<State>.AsyncAction action);
		Task<Result> Dispatch<Result>(Store<State>.IAsyncAction<Result> action);
		Task Dispatch(Store<State>.IAsyncAction action);
	}

	public class Store<State> : IDispatcher<State>
	{
        public delegate State GetStateDelegate();
        public delegate Task AsyncAction(DispatcherDelegate dispatcher, GetStateDelegate getState);
        public delegate Task<Result> AsyncAction<Result>(DispatcherDelegate dispatcher, GetStateDelegate getState);
        public delegate AsyncAction<Result> AsyncActionNeedsParam<T, Result>(T param); 
        public delegate AsyncAction AsyncActionNeedsParam<T>(T param);

		public interface IAsyncAction<Result>
		{
			Task<Result> Dispatch(IDispatcher<State> dispatcher, GetStateDelegate getState);
		}

		public interface IAsyncAction
		{
			Task Dispatch(IDispatcher<State> dispatcher, GetStateDelegate getState);
		}

        private readonly BasicStore store;
        private MiddlewareExecutor middlewares;

        public Store(SimpleReducer<State> rootReducer) : this(rootReducer.Get())
        {
        }

        public Store(CompositeReducer<State> rootReducer) : this(rootReducer.Get())
        {
        }

        public Store(Reducer<State> rootReducer)
        {
            store = new BasicStore(rootReducer);
            Middleware();
        }

        public Unsubscribe Subscribe(StateChangedSubscriber<State> subscription)
        {
            return store.Subscribe(subscription);
        }

        public void Dispatch(Object action)
        {
            middlewares(action);
        }

        public Task<Result> Dispatch<Result>(AsyncAction<Result> action)
        {
            return action(Dispatch, GetState);
        }

        public Task Dispatch(AsyncAction action)
        {
            return action(Dispatch, GetState);
        }

		public Task<Result> Dispatch<Result>(IAsyncAction<Result> action)
		{
			return action.Dispatch(this, GetState);
		}

		public Task Dispatch(IAsyncAction action)
		{
			return action.Dispatch(this, GetState);
		}

        public AsyncActionNeedsParam<T, Result> asyncAction<T, Result>(
            Func<DispatcherDelegate, GetStateDelegate, T, Task<Result>> action)
        {
            return invokeParam => (dispatch, getState) => action(dispatch, getState, invokeParam);
        }

        public AsyncActionNeedsParam<T> asyncActionVoid<T>(
            Func<DispatcherDelegate, GetStateDelegate, T, Task> action)
        {
            return invokeParam => (dispatch, getState) => action(dispatch, getState, invokeParam);
        }

        public AsyncAction<Result> asyncAction<Result>(
            AsyncAction<Result> action)
        {
            return (dispatch, getState) => action(dispatch, getState);
        }

        public State GetState()
        {
            return store.GetState();
        }

        public void Middleware(params Middleware<State>[] middlewares)
        {
            this.middlewares =
                middlewares.Select(m => m(store))
                    .Reverse()
                    .Aggregate<MiddlewareChainer, MiddlewareExecutor>(store.Dispatch, (acc, middle) => middle(acc));
        }

        private class BasicStore : IBasicStore<State>
        {
            private readonly Reducer<State> rootReducer;

            private readonly List<StateChangedSubscriber<State>> subscriptions =
                new List<StateChangedSubscriber<State>>();

            private State state;

            public BasicStore(Reducer<State> rootReducer)
            {
                this.rootReducer = rootReducer;
                state = rootReducer(state, new InitStoreAction());
            }

            public Unsubscribe Subscribe(StateChangedSubscriber<State> subscription)
            {
                subscriptions.Add(subscription);
                return () => { subscriptions.Remove(subscription); };
            }

            public void Dispatch(Object action)
            {
                state = rootReducer(state, action);
                foreach (var subscribtion in subscriptions)
                {
                    subscribtion(state);
                }
            }

            public State GetState()
            {
                return state;
            }
        }
    }

    public delegate void MiddlewareExecutor(Object action);
    public delegate MiddlewareExecutor MiddlewareChainer(MiddlewareExecutor nextMiddleware);
    public delegate MiddlewareChainer Middleware<State>(IBasicStore<State> store);
}