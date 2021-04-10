using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using MuTest.Firebase.Api.HealthChecks;
using MuTest.Firebase.Api.Services;

namespace MuTest.Firebase.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddControllers();
            services.AddTransient<DbHealthCheck>();
            services.AddHealthChecks().AddCheck<DbHealthCheck>("Database");
            services.AddSingleton<IDbService>(new DbService(Configuration, new AuthenticationService()));
            services.AddSingleton<IFirestoreService>(new FirestoreService(Configuration));
            services.AddSingleton<IStorageService>(new StorageService(Configuration, new AuthenticationService()));

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MuTest Firebase Api",
                    Version = "v1",
                    Contact = new OpenApiContact
                    {
                        Email = "AutomatedUnitTest@aurea.com",
                        Name = "Automation Team"
                    },
                    Description = "MuTest Database and Storage service to get and store data in Google Firebase"
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (env == null)
            {
                throw new ArgumentNullException(nameof(env));
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseSwagger();
            app.UseHealthChecks("/healthy");
            app.UseSwaggerUI(x =>
            {
                x.SwaggerEndpoint("/swagger/v1/swagger.json", "MuTest Firebase Api");
                x.RoutePrefix = string.Empty;
            });
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
