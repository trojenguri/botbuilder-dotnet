﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Bot.Builder.AI.QnA
{
    /// <summary>
    /// Helper class for train API
    /// </summary>
    internal class TrainHelper
    {
        private static readonly string QnAMakerName = nameof(QnAMaker);
        private QnAMakerEndpoint _endpoint;
        private readonly HttpClient httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrainHelper"/> class.
        /// </summary>
        /// <param name="endpoint">QnA Maker endpoint details.</param>
        /// <param name="httpClient">Http client.</param>
        public TrainHelper(QnAMakerEndpoint endpoint, HttpClient httpClient)
        {
            this._endpoint = endpoint;
            this.httpClient = httpClient;
        }

        /// <summary>
        /// Train API to provide feedback
        /// </summary>
        /// <param name="feedbackRecords">Feedback record list.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task CallTrainAsync(FeedbackRecords feedbackRecords)
        {
            if (feedbackRecords == null)
            {
                throw new ArgumentNullException(nameof(feedbackRecords), "Feedback records cannot be null.");
            }

            if (feedbackRecords.Records == null || feedbackRecords.Records.Length == 0)
            {
                return;
            }

            // Call train
            await this.QueryTrainAsync(feedbackRecords).ConfigureAwait(false);
        }

        private async Task QueryTrainAsync(FeedbackRecords feedbackRecords)
        {
            var requestUrl = $"{_endpoint.Host}/knowledgebases/{_endpoint.KnowledgeBaseId}/train";
            var jsonRequest = JsonConvert.SerializeObject(feedbackRecords, Formatting.None);

            var httpRequestHelper = new HttpRequestHelper(httpClient);
            var response = await httpRequestHelper.ExecuteHttpRequest(requestUrl, jsonRequest, _endpoint).ConfigureAwait(false);
        }
    }
}
