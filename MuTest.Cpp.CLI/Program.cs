using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using MuTest.Api.Clients.ServiceClients;
using MuTest.Core.Common;
using MuTest.Core.Common.Settings;
using MuTest.Core.Exceptions;
using MuTest.Core.Testing;
using MuTest.Cpp.CLI.Core;
using Polly;
using Polly.Extensions.Http;
using static System.Console;

namespace MuTest.Cpp.CLI
{
    public class Program
    {
        private static IChalk _chalk;
        private static IMuTestRunner _muTest;
        public static readonly MuTestSettings MuTestSettings = MuTestSettingsSection.GetSettings();

        private static int Main(string[] args)
        {
            try
            {
                _chalk = new Chalk();
                Trace.Listeners.Add(new EventLogTraceListener("MuTest_CPP_CLI"));
                var services = new ServiceCollection();

                services
                    .AddHttpClient<IFirebaseApiClient, FirebaseApiClient>()
                    .AddPolicyHandler(GetRetryPolicy())
                    .AddPolicyHandler(GetCircuitBreakerPatternPolicy());
                services.AddSingleton(_chalk);
                var provider = services.BuildServiceProvider();
                var factory = provider.GetService<IHttpClientFactory>();

                    _muTest = new MuTestRunner(
                        _chalk, 
                        new CppDirectoryFactory(),
                        new FirebaseApiClient(
                            factory.CreateClient(), 
                            MuTestSettings.FireBaseDatabaseApiUrl, 
                            MuTestSettings.FireBaseStorageApiUrl));
                var app = new MuTestCli(_muTest);

                CancelKeyPress += CancelMutationHandler;
                AppDomain.CurrentDomain.ProcessExit += CancelMutationHandler;

                return app.Run(args);
            }
            catch (MuTestInputException ex)
            {
                ShowMessage(ex);
				CancelMutation();
                return 1;
            }
            catch (CommandParsingException ex)
            {
                _chalk.Red(ex.Message);
                CancelMutation();
                return 1;
            }
            catch (Exception ex)
            {
                CancelMutation();
                var innerException = ex.InnerException;
                if (innerException != null && (innerException.GetType() == typeof(MuTestInputException) ||
                                               innerException.GetType().BaseType == typeof(MuTestInputException)))
                {
                    ShowMessage((MuTestInputException) innerException);

                    var statusCode = MuTestExceptions[innerException.GetType()];
                    _chalk.Red($"\nStatus Code: {statusCode}\n");
                    return statusCode;
                }

                if(ex.Message.StartsWith("Unrecognized option"))
                {
                    _chalk.Default($"{ex.Message}{Environment.NewLine}");
                    return 1;
                }

                Trace.TraceError("{0}", ex);
                _chalk.Red($"\n{ex.Message} - review trace for more details\n");
                return 1;
            }
        }

        private static readonly IDictionary<Type, int> MuTestExceptions = new Dictionary<Type, int>
        {
            [typeof(MuTestInputException)] = 1,
            [typeof(MuTestFailingTestException)] = 2,
            [typeof(MuTestFailingBuildException)] = 3
        };

        private static void CancelMutationHandler(object sender, EventArgs e)
        {
            CancelMutation();
        }

        private static void CancelMutation()
        {
            _muTest?.MutantsExecutor?.Stop();

            if (_muTest?.DirectoryFactory != null && _muTest.Context != null)
            {
                _muTest.DirectoryFactory.DeleteTestFiles(_muTest.Context);
            }
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPatternPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(2, TimeSpan.FromSeconds(30));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == HttpStatusCode.NotFound)
                .OrResult(msg => msg.StatusCode == HttpStatusCode.InternalServerError)
                .WaitAndRetryAsync(2, retryAttempy => TimeSpan.FromSeconds(Math.Pow(2, retryAttempy)));
        }

        private static void ShowMessage(MuTestInputException strEx)
        {
            _chalk.Yellow("\nMuTest C++ failed to mutate your project. For more information see the logs below:\n");
            _chalk.Red($"\n{strEx.Message} - {strEx.Details.ConvertToPlainText()}\n");
        }
    }
}