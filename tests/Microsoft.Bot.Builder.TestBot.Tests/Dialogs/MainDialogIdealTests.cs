﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Tests.Utils;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.BotBuilderSamples.Tests.Dialogs
{
    public class MainDialogIdealTests : DialogTestsBase
    {

        public MainDialogIdealTests(ITestOutputHelper output)
            : base(output)
        {
        }

        // Note: this test doesn't use a mock for booking so it ends up executing things that are out of scope from MainDialog.
        [Fact]
        public async Task WholeEnchilada()
        {
            // Arrange
            var sut = new MainDialog(MockConfig.Object, MockLogger.Object);
            var testBot = new DialogsTestBot(sut, Output);

            // Act/Assert
            var reply = await testBot.SendAsync<IMessageActivity>("Hi");
            Assert.Equal("What can I help you with today?", reply.Text);

            reply = await testBot.SendAsync<IMessageActivity>("irrelevant");
            Assert.Equal("Where would you like to travel to?", reply.Text);

            reply = await testBot.SendAsync<IMessageActivity>("Bahamas");
            Assert.Equal("Where are you traveling from?", reply.Text);

            reply = await testBot.SendAsync<IMessageActivity>("New York");
            Assert.Equal("When would you like to travel?", reply.Text);

            reply = await testBot.SendAsync<IMessageActivity>("tomorrow at 5 PM");
            Assert.Equal("Please confirm, I have you traveling to: Bahamas from: New York on: 2019-04-19T17 (1) Yes or (2) No", reply.Text);
        }

        [Theory]
        [InlineData("{\"Destination\":\"Bahamas\",\"Origin\":\"New York\",\"TravelDate\":\"2019-04-19\"}", "I have you booked to Bahamas from New York on tomorrow")]
        [InlineData(null, "Thank you.")]
        public async Task MainDialogWithMockBooking(string bookingResult, string endMessage)
        {
            // Arrange
            var expectedResult = bookingResult == null ? null : JsonConvert.DeserializeObject<BookingDetails>(bookingResult);
            var mockBookingDialog = DialogUtils.CreateMockDialog<BookingDialog>(expectedResult);

            var sut = new MainDialog(MockConfig.Object, MockLogger.Object, mockBookingDialog.Object);
            var testBot = new DialogsTestBot(sut, Output);

            // Act/Assert
            var reply = await testBot.SendAsync<IMessageActivity>("Hi");
            Assert.Equal("What can I help you with today?", reply.Text);

            reply = await testBot.SendAsync<IMessageActivity>("irrelevant");
            Assert.Equal("BookingDialog mock invoked", reply.Text);

            reply = await testBot.GetNextReplyAsync<IMessageActivity>();
            Assert.Equal(endMessage, reply.Text);
        }

        //[Fact]
        //public async Task TestSingleStep()
        //{
        //    //Arrange
        //    JObject Foo = new MyState("Set all my state here");
        //    MyDialog d = new MyDialog(Foo);

        //    Activity a = new New Activty("Hi");

        //    // ACt
        //    (RevisedState rs, OutputActivities oa) = RunDialogStep(MyDialog, Foo, a);

        //    //Assert
        //    Assert.rs[Targetcity] = "SFO"
        //    Assert.oa.Count()( = 2)
        //    Assert oa[0] = typeing
        //    Assert
        //}

        public static IEnumerable<object[]> MainDialogData =>
            new List<object[]>
            {
                new object[] { null, "Thank you." },
                new object[] { new BookingDetails { Destination = "BahXamas", Origin = "New York", TravelDate = $"{DateTime.UtcNow.AddDays(1):yyyy-MM-dd}"}, "I have you booked to Bahamas from New York on tomorrow" },
            };

        [Theory]
        [MemberData(nameof(MainDialogData))]
        public async Task MainDialogWithMockBookingAndMemberData(BookingDetails expectedResult, string endMessage)
        {
            // Arrange
            var mockBookingDialog = DialogUtils.CreateMockDialog<BookingDialog>(expectedResult);

            var sut = new MainDialog(MockConfig.Object, MockLogger.Object, mockBookingDialog.Object);
            var testBot = new DialogsTestBot(sut, Output);

            // Act/Assert
            var reply = await testBot.SendAsync<IMessageActivity>("Hi");
            Assert.Equal("What can I help you with today?", reply.Text);

            reply = await testBot.SendAsync<IMessageActivity>("irrelevant");
            Assert.Equal("BookingDialog mock invoked", reply.Text);

            reply = await testBot.GetNextReplyAsync<IMessageActivity>();
            Assert.Equal(endMessage, reply.Text);
        }

        [Fact]
        public void DialogConstructor()
        {
            // TODO: check with the team if there's value in these types of test or if there's a better way of asserting the
            // dialog got composed properly.
            var sut = new MainDialog(MockConfig.Object, MockLogger.Object);

            Assert.Equal("MainDialog", sut.Id);
            Assert.IsType<TextPrompt>(sut.FindDialog("TextPrompt"));
            Assert.IsType<BookingDialog>(sut.FindDialog("BookingDialog"));
            Assert.IsType<WaterfallDialog>(sut.FindDialog("WaterfallDialog"));
        }
    }
}