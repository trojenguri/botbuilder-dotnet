﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace Microsoft.Bot.Builder.AI.QnA
{
    /// <summary>
    /// Helper class for Generate Answer API.
    /// </summary>
    internal class GenerateAnswerHelper
    {
        private static readonly string QnAMakerName = nameof(QnAMaker);
        private readonly IBotTelemetryClient telemetryClient;
        private QnAMakerEndpoint _endpoint;
        private readonly HttpClient httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenerateAnswerHelper"/> class.
        /// </summary>
        /// <param name="telemetryClient">Telemetry client.</param>
        /// <param name="endpoint">QnA Maker endpoint details.</param>
        /// <param name="options">QnA Maker options.</param>
        /// <param name="httpClient">Http client.</param>
        /// <param name="logPersonalInformation">Log personal Information.</param>
        public GenerateAnswerHelper(IBotTelemetryClient telemetryClient, QnAMakerEndpoint endpoint, QnAMakerOptions options, HttpClient httpClient)
        {
            this.telemetryClient = telemetryClient;
            this._endpoint = endpoint;

            this.Options = options ?? new QnAMakerOptions();
            ValidateOptions(this.Options);
            this.httpClient = httpClient;
        }

        /// <summary>
        /// Gets or sets qnA Maker options.
        /// </summary>
        public QnAMakerOptions Options { get; set; }

        /// <summary>
        /// Generates an answer from the knowledge base.
        /// </summary>
        /// <param name="turnContext">The Turn Context that contains the user question to be queried against your knowledge base.</param>
        /// <param name="messageActivity">Message activity of the turn context.</param>
        /// <param name="options">The options for the QnA Maker knowledge base. If null, constructor option is used for this instance.</param>
        /// <returns>A list of answers for the user query, sorted in decreasing order of ranking score.</returns>
        public async Task<QueryResult[]> GetAnswersAsync(ITurnContext turnContext, IMessageActivity messageActivity, QnAMakerOptions options)
        {
            if (turnContext == null)
            {
                throw new ArgumentNullException(nameof(turnContext));
            }

            if (turnContext.Activity == null)
            {
                throw new ArgumentNullException(nameof(turnContext.Activity));
            }

            if (messageActivity == null)
            {
                throw new ArgumentException("Activity type is not a message");
            }

            var hydratedOptions = HydrateOptions(options);
            ValidateOptions(hydratedOptions);

            var result = await QueryQnaServiceAsync((Activity)messageActivity, hydratedOptions).ConfigureAwait(false);

            await EmitTraceInfoAsync(turnContext, (Activity)messageActivity, result, hydratedOptions).ConfigureAwait(false);

            return result;
        }

        private static async Task<QueryResult[]> FormatQnaResultAsync(HttpResponseMessage response, QnAMakerOptions options)
        {
            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var results = JsonConvert.DeserializeObject<QueryResults>(jsonResponse);

            foreach (var answer in results.Answers)
            {
                answer.Score = answer.Score / 100;
            }

            var result = results.Answers.Where(answer => answer.Score > options.ScoreThreshold).ToArray();

            return result;
        }

        private static void ValidateOptions(QnAMakerOptions options)
        {
            if (options.ScoreThreshold == 0)
            {
                options.ScoreThreshold = 0.3F;
            }

            if (options.Top == 0)
            {
                options.Top = 1;
            }

            if (options.ScoreThreshold < 0 || options.ScoreThreshold > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options.ScoreThreshold), "Score threshold should be a value between 0 and 1");
            }

            if (options.Timeout == 0.0D)
            {
                options.Timeout = 100000;
            }

            if (options.Top < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options.Top), "Top should be an integer greater than 0");
            }

            if (options.StrictFilters == null)
            {
                options.StrictFilters = new Metadata[] { };
            }

            if (options.MetadataBoost == null)
            {
                options.MetadataBoost = new Metadata[] { };
            }
        }

        /// <summary>
        /// Combines QnAMakerOptions passed into the QnAMaker constructor with the options passed as arguments into GetAnswersAsync().
        /// </summary>
        /// <param name="queryOptions">The options for the QnA Maker knowledge base.</param>
        /// <returns>Return modified options for the QnA Maker knowledge base.</returns>
        private QnAMakerOptions HydrateOptions(QnAMakerOptions queryOptions)
        {
            var hydratedOptions = JsonConvert.DeserializeObject<QnAMakerOptions>(JsonConvert.SerializeObject(this.Options));

            if (queryOptions != null)
            {
                if (queryOptions.ScoreThreshold != hydratedOptions.ScoreThreshold && queryOptions.ScoreThreshold != 0)
                {
                    hydratedOptions.ScoreThreshold = queryOptions.ScoreThreshold;
                }

                if (queryOptions.Top != hydratedOptions.Top && queryOptions.Top != 0)
                {
                    hydratedOptions.Top = queryOptions.Top;
                }

                if (queryOptions.StrictFilters?.Length > 0)
                {
                    hydratedOptions.StrictFilters = queryOptions.StrictFilters;
                }

                if (queryOptions.MetadataBoost?.Length > 0)
                {
                    hydratedOptions.MetadataBoost = queryOptions.MetadataBoost;
                }
            }

            return hydratedOptions;
        }

        private async Task<QueryResult[]> QueryQnaServiceAsync(Activity messageActivity, QnAMakerOptions options)
        {
            var requestUrl = $"{_endpoint.Host}/knowledgebases/{_endpoint.KnowledgeBaseId}/generateanswer";
            var jsonRequest = JsonConvert.SerializeObject(
                new
                {
                    question = messageActivity.Text,
                    top = options.Top,
                    strictFilters = options.StrictFilters,
                    metadataBoost = options.MetadataBoost,
                    scoreThreshold = options.ScoreThreshold,
                }, Formatting.None);

            var httpRequestHelper = new HttpRequestHelper(httpClient);
            var response = await httpRequestHelper.ExecuteHttpRequest(requestUrl, jsonRequest, _endpoint).ConfigureAwait(false);

            var result = await FormatQnaResultAsync(response, options).ConfigureAwait(false);

            return result;
        }

        private async Task EmitTraceInfoAsync(ITurnContext turnContext, Activity messageActivity, QueryResult[] result, QnAMakerOptions options)
        {
            var traceInfo = new QnAMakerTraceInfo
            {
                Message = (Activity)messageActivity,
                QueryResults = result,
                KnowledgeBaseId = _endpoint.KnowledgeBaseId,
                ScoreThreshold = options.ScoreThreshold,
                Top = options.Top,
                StrictFilters = options.StrictFilters,
                MetadataBoost = options.MetadataBoost,
            };
            var traceActivity = Activity.CreateTraceActivity(QnAMakerName, QnATelemetryConstants.QnAMakerTraceType, traceInfo, QnATelemetryConstants.QnAMakerTraceLabel);
            await turnContext.SendActivityAsync(traceActivity).ConfigureAwait(false);
        }
    }
}
