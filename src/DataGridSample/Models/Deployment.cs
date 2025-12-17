// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DataGridSample.Models
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)]
    public class Deployment
    {
        public Deployment(
            string service,
            string region,
            string ring,
            string status,
            DateTimeOffset started,
            double errorRate,
            int incidents)
        {
            Service = service;
            Region = region;
            Ring = ring;
            Status = status;
            Started = started;
            ErrorRate = errorRate;
            Incidents = incidents;
        }

        public string Service { get; }

        public string Region { get; }

        public string Ring { get; }

        public string Status { get; }

        public DateTimeOffset Started { get; }

        public double ErrorRate { get; }

        public int Incidents { get; }

        public static IEnumerable<Deployment> CreateSeed()
        {
            return new[]
            {
                new Deployment("Checkout-2", "us-east", "Canary", "Investigating", new DateTimeOffset(2024, 9, 15, 12, 45, 0, TimeSpan.Zero), 0.037, 3),
                new Deployment("Checkout-11", "us-east", "R1", "Rolling Out", new DateTimeOffset(2024, 9, 15, 8, 5, 0, TimeSpan.Zero), 0.012, 1),
                new Deployment("Identity-3", "eu-west", "R0", "Paused", new DateTimeOffset(2024, 9, 14, 22, 15, 0, TimeSpan.Zero), 0.021, 2),
                new Deployment("Identity-7", "eu-west", "R2", "Rolling Out", new DateTimeOffset(2024, 9, 14, 21, 5, 0, TimeSpan.Zero), 0.009, 0),
                new Deployment("Search-1", "apac-south", "R3", "Completed", new DateTimeOffset(2024, 9, 13, 18, 20, 0, TimeSpan.Zero), 0.004, 0),
                new Deployment("Search-10", "apac-south", "R1", "Rolling Out", new DateTimeOffset(2024, 9, 13, 20, 45, 0, TimeSpan.Zero), 0.011, 1),
                new Deployment("Fulfillment-4", "us-west", "Canary", "Blocked", new DateTimeOffset(2024, 9, 15, 5, 10, 0, TimeSpan.Zero), 0.052, 4),
                new Deployment("Fulfillment-12", "us-west", "R2", "Investigating", new DateTimeOffset(2024, 9, 15, 6, 30, 0, TimeSpan.Zero), 0.028, 2),
                new Deployment("Billing-5", "us-central", "R0", "Rolling Out", new DateTimeOffset(2024, 9, 14, 10, 40, 0, TimeSpan.Zero), 0.007, 0),
                new Deployment("Billing-14", "us-central", "R3", "Completed", new DateTimeOffset(2024, 9, 12, 15, 20, 0, TimeSpan.Zero), 0.003, 0),
                new Deployment("Analytics-6", "eu-north", "R1", "Paused", new DateTimeOffset(2024, 9, 14, 2, 15, 0, TimeSpan.Zero), 0.017, 1),
                new Deployment("Analytics-8", "eu-north", "R2", "Rolling Out", new DateTimeOffset(2024, 9, 14, 7, 30, 0, TimeSpan.Zero), 0.013, 1),
            };
        }
    }
}
