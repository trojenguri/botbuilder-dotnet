﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Tests.Framework;
using Xunit;

namespace Microsoft.BotBuilderSamples.Tests.Dialogs
{
    public class CancelAndHelpDialogTests
    {
        [Theory]
        [InlineData("hi", "Hi there", "cancel")]
        [InlineData("hi", "Hi there", "quit")]
        public async Task ShouldBeAbleToCancel(string utterance, string response, string cancelUtterance)
        {
            var sut = new TestCancelAndHelpDialog();
            var testClient = new DialogTestClient(sut);

            var reply = await testClient.SendAsync<IMessageActivity>(utterance);
            Assert.Equal(response, reply.Text);
            Assert.Equal(DialogTurnStatus.Waiting, testClient.DialogTurnResult.Status);

            reply = await testClient.SendAsync<IMessageActivity>(cancelUtterance);
            Assert.Equal("Cancelling", reply.Text);
            Assert.Equal(DialogTurnStatus.Cancelled, testClient.DialogTurnResult.Status);
        }

        [Theory]
        [InlineData("hi", "Hi there", "help")]
        [InlineData("hi", "Hi there", "?")]
        public async Task ShouldBeAbleToGetHelp(string utterance, string response, string cancelUtterance)
        {
            var sut = new TestCancelAndHelpDialog();
            var testClient = new DialogTestClient(sut);

            var reply = await testClient.SendAsync<IMessageActivity>(utterance);
            Assert.Equal(response, reply.Text);
            Assert.Equal(DialogTurnStatus.Waiting, testClient.DialogTurnResult.Status);

            reply = await testClient.SendAsync<IMessageActivity>(cancelUtterance);
            Assert.Equal("Show Help...", reply.Text);
            Assert.Equal(DialogTurnStatus.Waiting, testClient.DialogTurnResult.Status);
        }

        private class TestCancelAndHelpDialog : CancelAndHelpDialog
        {
            public TestCancelAndHelpDialog()
                : base(nameof(TestCancelAndHelpDialog))
            {
                AddDialog(new TextPrompt(nameof(TextPrompt)));
                var steps = new WaterfallStep[]
                {
                    PromptStep,
                    FinalStep,
                };
                AddDialog(new WaterfallDialog("testWaterfall", steps));
                InitialDialogId = "testWaterfall";
            }

            private async Task<DialogTurnResult> PromptStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
            {
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Hi there") }, cancellationToken);
            }

            private Task<DialogTurnResult> FinalStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
