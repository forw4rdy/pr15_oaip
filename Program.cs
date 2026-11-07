using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class OrderCancellingEventArgs : EventArgs
{
    public int OrderId;
    public decimal Amount;
    public string CustomerEmail;

    public bool Cancel { get; set; } = false;
    public string Reason { get; set; } = "";

    public OrderCancellingEventArgs(int orderId, decimal amount, string customerEmail)
    {
        OrderId = orderId;
        Amount = amount;
        CustomerEmail = customerEmail;
    }
}

class OrderPlacedEventArgs : EventArgs
{
    public int OrderId;
    public decimal Amount;
    public string CustomerEmail;

    public OrderPlacedEventArgs(int orderId, decimal amount, string customerEmail)
    {
        OrderId = orderId;
        Amount = amount;
        CustomerEmail = customerEmail;
    }
}

class OrderService
{
    public event EventHandler<OrderCancellingEventArgs>? OrderCancelling;
    public event EventHandler<OrderPlacedEventArgs>? OrderPlaced;

    public void PlaceOrder(int id, decimal amount, string customerEmail)
    {
        OrderCancellingEventArgs e =
            new OrderCancellingEventArgs(id, amount, customerEmail);

        if (OrderCancelling != null)
        {
            foreach (EventHandler<OrderCancellingEventArgs> handler in OrderCancelling.GetInvocationList())
            {
                try
                {
                    handler(this, e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        if (!e.Cancel)
        {
            OrderPlacedEventArgs placed =
                new OrderPlacedEventArgs(id, amount, customerEmail);

            if (OrderPlaced != null)
            {
                foreach (EventHandler<OrderPlacedEventArgs> handler in OrderPlaced.GetInvocationList())
                {
                    try
                    {
                        handler(this, placed);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            Console.WriteLine("Заказ #" + id + " оформлен");
        }
        else
        {
            Console.WriteLine("Заказ #" + id + " отменён: " + e.Reason);
        }
    }
}

class StockValidator
{
    public void CheckOrder(object? sender, OrderCancellingEventArgs e)
    {
        if (e.Amount > 20000)
        {
            e.Cancel = true;
            e.Reason = "Недостаточно товара на складе";
        }
    }
}

class FraudDetector
{
    public void CheckEmail(object? sender, OrderCancellingEventArgs e)
    {
        if (e.CustomerEmail.EndsWith(".test"))
        {
            e.Cancel = true;
            e.Reason = "Подозрительный адрес";
        }
    }
}

class AuditLog
{
    public void WriteLog(object? sender, OrderCancellingEventArgs e)
    {
        Console.WriteLine("[audit] попытка оформления #" + e.OrderId);
    }
}

sealed class EventAggregator
{
    private readonly Dictionary<Type, List<Delegate>> _handlers =
        new Dictionary<Type, List<Delegate>>();

    private readonly object _lock = new object();

    public void Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            Type type = typeof(T);

            if (!_handlers.ContainsKey(type))
            {
                _handlers[type] = new List<Delegate>();
            }

            _handlers[type].Add(handler);
        }
    }

    public void Unsubscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            Type type = typeof(T);

            if (_handlers.ContainsKey(type))
            {
                _handlers[type].Remove(handler);

                if (_handlers[type].Count == 0)
                {
                    _handlers.Remove(type);
                }
            }
        }
    }

    public void Publish<T>(T message)
    {
        Delegate[] handlers;

        lock (_lock)
        {
            if (_handlers.ContainsKey(typeof(T)))
            {
                handlers = _handlers[typeof(T)].ToArray();
            }
            else
            {
                handlers = Array.Empty<Delegate>();
            }
        }

        foreach (Delegate handler in handlers)
        {
            ((Action<T>)handler)(message);
        }
    }
}

record OrderPlaced(int Id, decimal Amount);

record TemperatureChanged(double New);

class EmailNotifier
{
    public void SendEmail(OrderPlaced order)
    {
        Console.WriteLine("[Email] Заказ #" + order.Id + " принят");
    }
}

class WarehouseService
{
    public void ProcessOrder(OrderPlaced order)
    {
        Console.WriteLine("[Склад] Заказ #" + order.Id + " отправлен на склад");
    }
}

class Alarm
{
    public void CheckTemperature(TemperatureChanged temperature)
    {
        Console.WriteLine("[Alarm] Температура: " + temperature.New);
    }
}

class LongLivedPublisher
{
    public event EventHandler? Tick;

    public void Raise()
    {
        Tick?.Invoke(this, EventArgs.Empty);
    }
}

class Subscriber
{
    public void OnTick(object? sender, EventArgs e)
    {
        Console.WriteLine("Подписчик получил событие");
    }
}

class DisposableSubscriber : IDisposable
{
    private LongLivedPublisher publisher;

    public DisposableSubscriber(LongLivedPublisher publisher)
    {
        this.publisher = publisher;
        publisher.Tick += OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Console.WriteLine("DisposableSubscriber получил событие");
    }

    public void Dispose()
    {
        publisher.Tick -= OnTick;
    }
}

class Program
{
    static WeakReference CreateLeakingSubscriber(LongLivedPublisher pub)
    {
        Subscriber sub = new Subscriber();
        pub.Tick += sub.OnTick;

        return new WeakReference(sub);
    }

    static WeakReference CreateDisposableSubscriber(LongLivedPublisher pub)
    {
        WeakReference weakRef;

        using (DisposableSubscriber sub = new DisposableSubscriber(pub))
        {
            weakRef = new WeakReference(sub);
        }

        return weakRef;
    }

    static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    static void Main()
    {
        Console.WriteLine("Задание 1. Отмена заказа через событие");

        OrderService service = new OrderService();

        StockValidator stock = new StockValidator();
        FraudDetector fraud = new FraudDetector();
        AuditLog audit = new AuditLog();

        service.OrderCancelling += stock.CheckOrder;
        service.OrderCancelling += fraud.CheckEmail;
        service.OrderCancelling += audit.WriteLog;

        service.OrderPlaced += (sender, e) =>
        {
            Console.WriteLine("Заказ #" + e.OrderId + " передан на оформление");
        };

        service.PlaceOrder(1, 5000, "user@mail.com");
        service.PlaceOrder(2, 25000, "user@mail.com");
        service.PlaceOrder(3, 15000, "user@mail.test");

        Console.WriteLine();
        Console.WriteLine("Задание 2. EventAggregator");

        EventAggregator agg = new EventAggregator();

        EmailNotifier email = new EmailNotifier();
        WarehouseService warehouse = new WarehouseService();
        Alarm alarm = new Alarm();

        agg.Subscribe<OrderPlaced>(email.SendEmail);
        agg.Subscribe<OrderPlaced>(warehouse.ProcessOrder);
        agg.Subscribe<TemperatureChanged>(alarm.CheckTemperature);

        agg.Publish(new OrderPlaced(1, 5000));
        agg.Publish(new TemperatureChanged(25.5));

        agg.Unsubscribe<OrderPlaced>(warehouse.ProcessOrder);

        agg.Publish(new OrderPlaced(2, 15000));

        Parallel.For(0, 1000, i =>
        {
            agg.Publish(new OrderPlaced(i, i));
        });

        Console.WriteLine("Параллельная публикация завершена");

        Console.WriteLine();
        Console.WriteLine("Задание 3. Утечка памяти при подписке на события");

        LongLivedPublisher pub = new LongLivedPublisher();

        WeakReference weakRef1 = CreateLeakingSubscriber(pub);

        CollectGarbage();

        Console.WriteLine("Подписчик жив: " + weakRef1.IsAlive);

        WeakReference weakRef2 = CreateDisposableSubscriber(pub);

        CollectGarbage();

        Console.WriteLine("Подписчик после Dispose жив: " + weakRef2.IsAlive);

        Console.ReadKey();
    }
}
