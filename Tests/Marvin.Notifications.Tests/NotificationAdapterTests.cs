﻿using System;
using Moq;
using NUnit.Framework;

namespace Marvin.Notifications.Tests
{
    /// <summary>
    /// Unittests for <see cref="NotificationAdapter"/>
    /// </summary>
    [TestFixture]
    public class NotificationAdapterTests
    {
        private INotificationAdapter _notificationAdapter;
        private Mock<INotificationSender> _notificationSenderMock;
        private INotificationSender _publishedEventSender;
        private IManagedNotification _publishedEventNotification;
        private IManagedNotification _acknowledgedEventNotification;
        private INotification _acknowledgeCallNotification;
        private INotificationContext _notificationpublisher;

        /// <summary>
        /// Initialize the test-environment
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _notificationAdapter = new NotificationAdapter();
            _notificationSenderMock = new Mock<INotificationSender>();
            _notificationSenderMock.Setup(n => n.Acknowledge(It.IsAny<INotification>()))
                .Callback((INotification notification) => _acknowledgeCallNotification = notification);
            _notificationSenderMock.SetupGet(n => n.Identifier).Returns("Mock");

            _publishedEventSender = null;
            _publishedEventNotification = null;
            _acknowledgedEventNotification = null;
            _acknowledgeCallNotification = null;

            ((INotificationSenderAdapter)_notificationAdapter).Published += (sender, notification) =>
            {
                _publishedEventSender = sender as INotificationSender;
                _publishedEventNotification = (IManagedNotification)notification;
            };

            ((INotificationSenderAdapter)_notificationAdapter).Acknowledged += (sender, notification) =>
            {
                _acknowledgedEventNotification = (IManagedNotification)notification;
            };

            _notificationpublisher = _notificationAdapter.Register(_notificationSenderMock.Object);
        }

        /// <summary>
        /// Check that publishing a notification publishes an event, and marks the notification as published. Check, that notifications can not be published twice.
        /// </summary>
        [Test(Description = "Check that publishing a notification publishes an event, and marks the notification as published. Check, that notifications can not be published twice.")]
        public void TestINotificationPublisherAdapterPublish()
        {
            // Arrange
            var notification = new Notification();

            // Act
            _notificationpublisher.Publish(notification);

            // Assert
            Assert.NotNull(_publishedEventNotification, "Published-event was not triggered.");
            Assert.AreEqual(_notificationSenderMock.Object, _publishedEventSender, "Published-event was triggered for wrong sender.");
            Assert.AreEqual(notification, _publishedEventNotification, "Published-event was triggered with wrong notification.");
            Assert.NotNull(_publishedEventNotification.Identifier, "Identifier should not be null.");
            Assert.AreNotEqual(_publishedEventNotification.Created, default(DateTime), "Created date should have been set");

            Assert.Throws<InvalidOperationException>(delegate
            {
                _notificationpublisher.Publish(notification);
            }, "The same notification was published a second time.");
        }

        /// <summary>
        /// Check that acknowledging a notification by the adapter for a known notification.
        /// </summary>
        [Test(Description = "Check that acknowledging a notification by the adapter for a known notification.")]
        public void TestINotificationPublisherAdapterAcknowledgeForKnownNotification()
        {
            // Arrange
            var notification = new Notification();
            _notificationpublisher.Publish(notification);

            // Act
            _notificationpublisher.Acknowledge(notification);

            // Assert
            Assert.NotNull(_acknowledgedEventNotification, "Acknowledged-event was not triggered.");
            Assert.AreEqual(notification, _acknowledgedEventNotification, "Acknowledged-event was triggered with wrong notification.");
            Assert.AreNotEqual(_acknowledgedEventNotification.Acknowledged, default(DateTime), "Acknowledged date should have been set");
        }

        [Test(Description = "Check that acknowledging a notification by the adapter for a known notification.")]
        public void AcknowledgeAKnownNotificationWhichIsAlreadyPublishedToThePublisher()
        {
            // Arrange
            var notification = new Notification();
            _notificationpublisher.Publish(notification);
            ((INotificationSenderAdapter)_notificationAdapter).PublishProcessed(notification);

            // Act
            _notificationpublisher.Acknowledge(notification);

            // Assert
            Assert.NotNull(_acknowledgedEventNotification, "Acknowledged-event was not triggered.");
            Assert.AreEqual(notification, _acknowledgedEventNotification, "Acknowledged-event was triggered with wrong notification.");
            Assert.AreNotEqual(_acknowledgedEventNotification.Acknowledged, default(DateTime), "Acknowledged date should have been set");
        }

        /// <summary>
        /// Check that acknowledging a notification by the adapter for a unknown notification throws an exception.
        /// </summary>
        [Test(Description = "Check that acknowledging a notification by the adapter for a known notification throws an exception.")]
        public void TestINotificationPublisherAdapterAcknowledgeForUnknownNotification()
        {
            // Arrange
            var notification = new Notification();

            // Act && Assert
            Assert.Throws<InvalidOperationException>(delegate
            {
                _notificationpublisher.Acknowledge(notification);
            }, "Acknowledge was called for an unknown notification");
        }

        /// <summary>
        /// Check that acknowledging a notification by the SenderAdapter-interface for a known notification is delegated to the original sender.
        /// </summary>
        [Test(Description = "Check that acknowledging a notification by the SenderAdapter-interface for a known notification is delegated to the original sender.")]
        public void TestINotificationSenderAdapterAcknowledgeForKnownNotification()
        {
            // Arrange
            var notification = new Notification();

            _notificationpublisher.Publish(notification);

            ((INotificationSenderAdapter)_notificationAdapter).PublishProcessed(notification);

            // Act
            ((INotificationSenderAdapter)_notificationAdapter).Acknowledge(notification);

            //Assert
            Assert.NotNull(_acknowledgeCallNotification, "Acknowledged was not called on the sender.");
            Assert.AreEqual(notification, _acknowledgeCallNotification, "Acknowledged was not called for the wrong notification.");
        }

        /// <summary>
        /// Check that acknowledging a notification by the SenderAdapter-interface for an unknown notification throws an exception.
        /// </summary>
        [Test(Description = "Check that acknowledging a notification by the SenderAdapter-interface for an unknown notification throws an exception.")]
        public void TestINotificationSenderAdapterAcknowledgeForUnknownNotification()
        {
            // Arrange
            var notification = new Notification();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(delegate
            {
                ((INotificationSenderAdapter)_notificationAdapter).Acknowledge(notification);
            }, "Acknowledge was called for an unknown notification");
        }

        [Test(Description = "Pending published notifications should be published again during a sync because of a restart of the Publisher")]
        public void PublishPendingNotificationsDuringTheSync()
        {
            // Arrange
            _notificationpublisher.Publish(new Notification());
            _notificationpublisher.Publish(new Notification());
            _notificationpublisher.Publish(new Notification());
            _notificationpublisher.Publish(new Notification());
            int counter = 0;
            ((INotificationSenderAdapter) _notificationAdapter).Published += delegate { counter += 1; };

            // Act

            ((INotificationSenderAdapter) _notificationAdapter).Sync();

            // Assert
            Assert.AreEqual(4, counter, "There should be four publish events. One for each pending notification");

        }

        [Test(Description = "Pending acknowledgements should be acknowledged again during a sync because of a restart of the Publisher")]
        public void AcknowledgePendingAcknowledgementsDuringTheSync()
        {
            // Arrange
            var notifiaction1 = new Notification();
            var notifiaction2 = new Notification();

            _notificationpublisher.Publish(notifiaction1);
            _notificationpublisher.Publish(notifiaction2);
            _notificationpublisher.Acknowledge(notifiaction1);
            _notificationpublisher.Acknowledge(notifiaction2);
            int counter = 0;
            ((INotificationSenderAdapter) _notificationAdapter).Acknowledged += delegate { counter += 1; };

            // Act
            ((INotificationSenderAdapter) _notificationAdapter).Sync();

            // Assert
            Assert.AreEqual(2, counter, "There should be two ackowledge events. One for each pending acknowledgement which should be synchronized with the Publisher");
        }

        [Test(Description = "Nothing to do during the synchronization if everything is up to date")]
        public void NothingToDoIfEverythingIsUpToDate()
        {
            // Arrange
            var notifiaction1 = new Notification();
            var notification2 = new Notification();
            _notificationpublisher.Publish(notifiaction1);
            _notificationpublisher.Publish(notification2);
            ((INotificationSenderAdapter)_notificationAdapter).PublishProcessed(notifiaction1);
            ((INotificationSenderAdapter)_notificationAdapter).PublishProcessed(notification2);
            int counter = 0;
            ((INotificationSenderAdapter) _notificationAdapter).Published += delegate { counter += 1; };

            // Act
            ((INotificationSenderAdapter) _notificationAdapter).Sync();

            // Assert
            Assert.AreEqual(0, counter, "There should be no publish events because everything should be up to date");
        }

        [Test(Description = "Check that acknowledging a notification by the SenderAdapter-interface for an unknown notification throws an exception.")]
        public void ThrowExceptionIfTheSenderPublishesANotificationButIsNotRegistered()
        {
            // Arrange
            var notification = new Notification();
            _notificationAdapter.Unregister(_notificationSenderMock.Object);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(delegate
            {
                _notificationpublisher.Publish(notification);
            });
        }
    }
}