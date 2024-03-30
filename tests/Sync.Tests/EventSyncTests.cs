namespace Sync.Tests;

internal class MyEvent
{
    public int Ticket { get; set; }

    public string Type { get; set; } = "Foo";
}

internal class EventPublisher
{
    public event EventHandler<MyEvent>? ExampleEvent;

    public void Publish(MyEvent e)
    {
        ExampleEvent?.Invoke(this, e);
    }
}

public class EventSyncTests
{
    [Fact]
    public async Task ReturnsDefault_WhenNoEventReceived()
    {
        int ticket = 0;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.WaitForEvent(TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public async Task ReturnsValue_WhenTicketMatches()
    {
        int ticket = 42;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket);

        sync.OnEvent(new MyEvent() { Ticket = 42 });

        var result = await sync.WaitForEvent(TimeSpan.FromMilliseconds(10));

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ReturnsDefault_WhenTicketChanged()
    {
        int ticket = 42;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket);

        ticket = 45;
        sync.OnEvent(new MyEvent() { Ticket = 42 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.WaitForEvent(TimeSpan.FromMilliseconds(10)));
    }


    [Fact]
    public async Task ReturnsValue_WhenTicketChangedToExpected()
    {
        int ticket = 0;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket);

        ticket = 45;
        sync.OnEvent(new MyEvent() { Ticket = 44 });
        sync.OnEvent(new MyEvent() { Ticket = 45 });
        sync.OnEvent(new MyEvent() { Ticket = 46 });

        var result = await sync.WaitForEvent(TimeSpan.FromMilliseconds(10));

        Assert.NotNull(result);
        Assert.Equal(45, result.Ticket);
    }


    [Fact]
    public async Task ReturnsValue_WhenTicketChangedToExpectedByTask()
    {
        int ticket = 0;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket);

        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 45 }));
        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 44 }));
        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 46 }));
        _ = Task.Run(() => ticket = 45);

        var result = await sync.WaitForEvent(TimeSpan.FromMilliseconds(1000));

        Assert.NotNull(result);
        Assert.Equal(45, result.Ticket);
    }

    [Fact]
    public async Task ReturnsDefault_WhenTicketMatchesButEventFiltered()
    {
        int ticket = 0;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket, e => !"Foo".Equals(e.Type));

        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 45, Type = "Bar" }));
        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 44 }));
        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 46 }));
        _ = Task.Run(() => ticket = 45);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.WaitForEvent(TimeSpan.FromMilliseconds(1000)));
    }

    [Fact]
    public async Task ReturnsValue_WhenTicketMatchesAndEventNotFiltered()
    {
        int ticket = 0;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket, e => !"Bar".Equals(e.Type));

        var task = sync.WaitForEvent(TimeSpan.FromMilliseconds(1000));

        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 45, Type = "Bar" }));
        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 44 }));
        _ = Task.Run(() => sync.OnEvent(new MyEvent() { Ticket = 46 }));
        _ = Task.Run(() => ticket = 45);

        var result = await task;

        Assert.NotNull(result);
        Assert.Equal(45, result.Ticket);
    }

    [Fact]
    public async Task EventHandlerTest_ReturnsValue_WhenTicketChangedToExpectedByTask()
    {
        var publisher = new EventPublisher();
        int ticket = 0;
        using var sync = new EventSync<MyEvent>(e => e.Ticket == ticket);
        publisher.ExampleEvent += sync.OnEvent;

        _ = Task.Run(() => publisher.Publish(new MyEvent() { Ticket = 45 }));
        _ = Task.Run(() => publisher.Publish(new MyEvent() { Ticket = 44 }));
        _ = Task.Run(() => publisher.Publish(new MyEvent() { Ticket = 46 }));
        await Task.Delay(200);
        _ = Task.Run(() => ticket = 45);

        var result = await sync.WaitForEvent(TimeSpan.FromMilliseconds(1000));

        Assert.NotNull(result);
        Assert.Equal(45, result.Ticket);
    }
}