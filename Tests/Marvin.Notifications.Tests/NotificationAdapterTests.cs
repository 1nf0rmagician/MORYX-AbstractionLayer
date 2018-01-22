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
        private NotificationAdapter _notificationAdapter;
        private Mock<INotificationSender> _notificationSenderMock;
        private INotificationSender _publishedEventSender;
        private IManagedNotification _publishedEventNotification;
        private IManagedNotification _acknowledgedEventNotification;
        private INotification _acknowledgeCallNotification;

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

            _notificationAdapter.Published += (sender, notification) =>
            {
                _publishedEventSender = sender as INotificationSender;
                _publishedEventNotification = (IManagedNotification)notification;

            };

            _notificationAdapter.Acknowledged += (sender, notification) =>
            {
                _acknowledgedEventNotification = (IManagedNotification)notification;
            };

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
            ((INotificationAdapter)_notificationAdapter).Publish(_notificationSenderMock.Object, notification);

            // Assert
            Assert.NotNull(_publishedEventNotification, "Published-event was not triggered.");
            Assert.AreEqual(_notificationSenderMock.Object, _publishedEventSender, "Published-event was triggered for wrong sender.");
            Assert.AreEqual(notification, _publishedEventNotification, "Published-event was triggered with wrong notification.");
            Assert.NotNull(_publishedEventNotification.Identifier, "Identifier should not be null.");
            Assert.AreNotEqual(_publishedEventNotification.Created, default(DateTime), "Created date should have been set");

            Assert.Throws<InvalidOperationException>(delegate
            {
                ((INotificationAdapter)_notificationAdapter).Publish(_notificationSenderMock.Object, notification);
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
            ((INotificationAdapter)_notificationAdapter).Publish(_notificationSenderMock.Object, notification);

            // Act
            ((INotificationAdapter)_notificationAdapter).Acknowledge(notification);

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
                ((INotificationAdapter) _notificationAdapter).Acknowledge(notification);
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

            ((INotificationAdapter)_notificationAdapter).Publish(_notificationSenderMock.Object, notification);

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
    }
}
