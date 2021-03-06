﻿using System;
using Moq;
using NUnit.Framework;
using NUnitReporter.EventReport;
using NUnitReporter.EventReport.ProxyFactory;

namespace NUnitReporterTests.Support
{
    public class ReportingProxyFactoryTests
    {
        private Mock<IEventReport> _eventReportMock;
        private ReportingProxyFactory _proxyFactory;
        private Mock<IEventReportFactory> _reportFactoryMock;

        [SetUp]
        public void SetUpMocks()
        {
            _reportFactoryMock = new Mock<IEventReportFactory>();
            _eventReportMock = new Mock<IEventReport>();
            _proxyFactory = new ReportingProxyFactory(_reportFactoryMock.Object);

            _reportFactoryMock
                .Setup(f => f.CurrentTestReport)
                .Returns(_eventReportMock.Object);
        }

        [Test]
        public void ProxyShouldTrackVirtualMethodUsage()
        {
            _eventReportMock
                .Setup(r => r.RecordActivityStarted("Phone::Call", "123456"))
                .Returns("abcdefg")
                .Verifiable();

            _eventReportMock
                .Setup(r => r.RecordActivityStarted("Phone::Call", "098765"))
                .Returns("hklmnop")
                .Verifiable();

            _eventReportMock
                .Setup(r => r.RecordActivityStarted("Phone::Charge"))
                .Returns("qprstxy")
                .Verifiable();

            _eventReportMock
                .Setup(r => r.RecordActivityFinished("qprstxy"))
                .Verifiable();

            _eventReportMock
                .Setup(r => r.RecordActivityFinished("hklmnop"))
                .Verifiable();

            _eventReportMock
                .Setup(r => r.RecordActivityFinished("abcdefg"))
                .Verifiable();

            var phoneMock = new Mock<IPhone>();

            var phoneProxy = _proxyFactory.Create<Phone>(phoneMock.Object);

            phoneProxy.Call("123456");
            phoneProxy.Call("098765");
            phoneProxy.Charge();

            phoneMock.Verify(p => p.Call("123456"));
            phoneMock.Verify(p => p.Call("098765"));
            phoneMock.Verify(p => p.Charge());

            _eventReportMock.Verify();
        }

        [Test]
        public void ProxyShouldRecordAnError()
        {
            var phoneMock = new Mock<IPhone>();

            var exception = new Exception("Low battery level");

            phoneMock.Setup(p => p.Call("123456")).Throws(exception);

            var phoneProxy = _proxyFactory.Create<Phone>(phoneMock.Object);

            Assert.That(() => phoneProxy.Call("123456"), Throws.Exception);

            _eventReportMock.Verify(r => r.RecordError(exception));
        }

        [Test]
        public void ProxyShouldRecordPropertyUsage()
        {
            _eventReportMock
                .Setup(r => r.RecordActivityStarted("Phone::SetOwner", "Bill Gates"))
                .Returns("kkjjll")
                .Verifiable();

            _eventReportMock
                .Setup(r => r.RecordActivityFinished("kkjjll"))
                .Verifiable();


            var phoneMock = new Mock<IPhone>();
            phoneMock.SetupProperty(p => p.Owner);

            var phoneProxy = _proxyFactory.Create<Phone>(phoneMock.Object);
            phoneProxy.Owner = "Bill Gates";
            
            Assert.That(phoneProxy.Owner, Is.EqualTo("Bill Gates"));
            Assert.That(phoneMock.Object.Owner, Is.EqualTo("Bill Gates"));

            _eventReportMock.Verify();
        }

        [Test]
        public void ProxyShouldIgnoreNonVirtualProperties()
        {
            var phoneMock = new Mock<IPhone>();

            var phoneProxy = _proxyFactory.Create<Phone>(phoneMock.Object);

            phoneProxy.DeviceId = "12345";

            Assert.That(phoneProxy.DeviceId, Is.EqualTo("12345"));

            _eventReportMock.Verify(r => r.RecordActivityStarted("Phone::SetDeviceId", "12345"), Times.Never);
        }

        [Test]
        public void ProxyShouldIgnoreBaseOperations()
        {
            var phoneMock = new Mock<IPhone>();

            var phoneProxy = _proxyFactory.Create<Phone>(phoneMock.Object);

            phoneProxy.ToString();
            phoneProxy.GetHashCode();

            _eventReportMock.Verify(r => r.RecordActivityStarted("ToString"), Times.Never);
            _eventReportMock.Verify(r => r.RecordActivityStarted("GetHashCode"), Times.Never);
        }

        [Test]
        public void ShouldNotReportIgnoredMethods()
        {
            var phoneMock = new Mock<IPhone>();
            var phoneProxy = _proxyFactory.Create<Phone>(phoneMock.Object);

            phoneProxy.SendMessage();

            phoneMock.Verify(p => p.SendMessage(), Times.Once);

            _eventReportMock.Verify(r => r.RecordActivityStarted("SendMessage"), Times.Never);
        }
    }

    public interface IPhone
    {
        void Call(string phone);
        void Charge();
        void SendMessage();

        String Owner { get; set; }
    }

    public class Phone : IPhone
    {
        private readonly IPhone _wrappedPhone;

        public virtual string Owner
        {
            get { return _wrappedPhone.Owner; }
            set { _wrappedPhone.Owner = value; }
        }

        public String DeviceId { get; set; }

        public Phone(IPhone wrappedPhone)
        {
            _wrappedPhone = wrappedPhone;
        }

        public virtual void Call(string phone)
        {
            _wrappedPhone.Call(phone);
        }

        public virtual void Charge()
        {
            _wrappedPhone.Charge();
        }

        [EventReport(Ignore = true)]
        public virtual void SendMessage()
        {
            _wrappedPhone.SendMessage();
        }

    }
}