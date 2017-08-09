﻿using Confluent.Kafka;
using Confluent.Kafka.Serialization;
using Jambo.Domain.Events;
using Jambo.Domain.SeedWork;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Jambo.KafkaBus
{
    public class ServiceBus : IServiceBus
    {
        private readonly string _topicName;

        private readonly Producer<string, string> _producer;
        private readonly Consumer<string, string> _consumer;

        private IList<ProcessDomainEventDelegate> _subscribers;

        public ServiceBus(string brokerList, string topicName)
        {
            _topicName = topicName;

            _producer = new Producer<string, string>(
                new Dictionary<string, object>()
                {{
                    "bootstrap.servers", brokerList}}, 
                new StringSerializer(Encoding.UTF8), new StringSerializer(Encoding.UTF8));

            _consumer = new Consumer<string, string>(
                new Dictionary<string, object>()
                {{ "group.id", "simple-csharp-consumer" },
                    { "bootstrap.servers", brokerList }},
                new StringDeserializer(Encoding.UTF8), new StringDeserializer(Encoding.UTF8));
        }

        public void AddSubscriber(ProcessDomainEventDelegate subscriber)
        {
            _subscribers = _subscribers ?? new List<ProcessDomainEventDelegate>();
            _subscribers.Add(subscriber);
        }

        public async Task Publish(IEvent _event)
        {
            string data = JsonConvert.SerializeObject(_event);

            Message<string, string> message = await _producer.ProduceAsync(
                _topicName, _event.ToString(), data);
        }

        public async Task Listen()
        {
            await Task.Run(() =>
            {
                _consumer.Assign(new List<TopicPartitionOffset>
                {
                    new TopicPartitionOffset(_topicName, 0, 0)
                });

                while (true)
                {
                    Message<string, string> msg;

                    if (_consumer.Consume(out msg, TimeSpan.FromSeconds(1)))
                    {
                        foreach (ProcessDomainEventDelegate eventDelegate in _subscribers)
                        {
                            eventDelegate(msg.Topic, msg.Partition, msg.Offset.Value, msg.Value);
                        }
                    }
                }
            });
        }

        public void Dispose()
        {
            _producer.Dispose();
            _consumer.Dispose();
        }
    }
}
