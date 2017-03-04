using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Reducto.Tests
{
    public struct LoginInfo
    {
        public string username;
    }

	public struct AsyncAction1 : Store<List<string>>.IAsyncAction
	{
		public async Task Dispatch(IDispatcher<List<string>> d, Store<List<string>>.GetStateDelegate getState)
		{
			await Task.Delay(300);
			Assert.That(getState()[0], Is.EqualTo("a"));
			d.Dispatch(new SomeAction());
		}
	}

	public struct AsyncAction2 : Store<List<string>>.IAsyncAction<int>
	{
		public int Param { get; set; }

		public async Task<int> Dispatch(IDispatcher<List<string>> d, Store<List<string>>.GetStateDelegate getState)
		{
			await Task.Delay(300);
			Assert.That(getState()[0], Is.EqualTo("a"));
			d.Dispatch(new SomeAction());
			return Param;
		}
	}

	public struct AsyncAction3 : Store<List<string>>.IAsyncAction<int>
	{
		public int Param { get; set; }

		public async Task<int> Dispatch(IDispatcher<List<string>> d, Store<List<string>>.GetStateDelegate getState)
		{
			var result = await d.Dispatch(new AsyncAction2 { Param = Param });

			await Task.Delay(300);
			Assert.That(getState()[0], Is.EqualTo("a"));
			d.Dispatch(new SomeAction());
			return Param + result;
		}
	}

	[TestFixture]
    public class AsyncActions
    {
        [Test]
        public async Task should_allow_for_async_execution_of_code()
        {
            var storeReducerReached = 0;
            var reducer = new SimpleReducer<List<string>>(() => new List<string> {"a"}).When<SomeAction>((s, e) =>
            {
                storeReducerReached += 1;
                return s;
            });
            var store = new Store<List<string>>(reducer);

            var result = await store.Dispatch(store.asyncAction<int>(async (dispatcher, store2) =>
            {
                await Task.Delay(300);
                Assert.That(store2()[0], Is.EqualTo("a"));
                dispatcher(new SomeAction());
                return 112;
            }));

            Assert.That(storeReducerReached, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(112));

			await store.Dispatch(new AsyncAction1());

			Assert.That(storeReducerReached, Is.EqualTo(2));
		}

        [Test]
        public async Task should_allow_for_passing_parameters_to_async_actions()
        {
            var storeReducerReached = 0;
            var reducer = new SimpleReducer<List<string>>(() => new List<string> {"a"}).When<SomeAction>((s, e) =>
            {
                storeReducerReached += 1;
                return s;
            });
            var store = new Store<List<string>>(reducer);

            var action1 = store.asyncAction<LoginInfo, int>(async (dispatcher, store2, msg) =>
            {
                await Task.Delay(300);
                Assert.That(msg.username, Is.EqualTo("John"));
                dispatcher(new SomeAction());
                return 112;
            });
            var result = await store.Dispatch(action1(new LoginInfo
            {
                username = "John"
            }));

            Assert.That(storeReducerReached, Is.EqualTo(1));
            Assert.That(result, Is.EqualTo(112));

			var result2 = await store.Dispatch(new AsyncAction2 { Param = 60 });

			Assert.That(storeReducerReached, Is.EqualTo(2));
			Assert.That(result2, Is.EqualTo(60));
		}

		[Test]
		public async Task should_execute_async_action_inside_async_action()
		{
			var storeReducerReached = 0;
			var reducer = new SimpleReducer<List<string>>(() => new List<string> { "a" }).When<SomeAction>((s, e) =>
			{
				storeReducerReached += 1;
				return s;
			});
			var store = new Store<List<string>>(reducer);

			var result = await store.Dispatch(new AsyncAction3 { Param = 60 });

			Assert.That(storeReducerReached, Is.EqualTo(2));
			Assert.That(result, Is.EqualTo(120));
		}
	}
}