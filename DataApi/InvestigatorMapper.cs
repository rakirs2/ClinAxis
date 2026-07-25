using System.Text.Json;
using Scrapers.Persistence.Entities;

namespace DataApi;

internal static class InvestigatorMapper
{
    internal static object ToSummary(InvestigatorPersonEntity person)
    {
        return new
        {
            uuid = person.Id,
            name = person.FullName,
            orcid = person.Orcid,
            ncbiId = person.NcbiId,
            studyCount = person.StudyInvestigators?.Count ?? 0,
            paperCount = person.InvestigatorPapers?.Count ?? 0,
            primaryAffiliation = person.Affiliations?.FirstOrDefault(a => a.IsPrimary)?.InstitutionName
        };
    }

    internal static object ToDetail(InvestigatorPersonEntity person, IEnumerable<StudyEntity> studies)
    {
        var studyList = studies.ToList();

        var coInvestigators = new HashSet<string>();
        foreach (var study in studyList)
        {
            if (study.StudyInvestigators != null)
            {
                foreach (var si in study.StudyInvestigators)
                {
                    if (si.InvestigatorPerson?.FullName != null && si.InvestigatorPerson.FullName != person.FullName)
                    {
                        coInvestigators.Add(si.InvestigatorPerson.FullName);
                    }
                }
            }
        }

        var conditions = new HashSet<string>();
        foreach (var study in studyList)
        {
            if (study.Conditions != null)
            {
                foreach (var cond in study.Conditions)
                {
                    if (cond.MeshDescriptor?.Name != null)
                        conditions.Add(cond.MeshDescriptor.Name);
                }
            }
        }

        var statuses = new Dictionary<string, int>();
        var phases = new Dictionary<string, int>();
        foreach (var study in studyList)
        {
            if (!string.IsNullOrEmpty(study.OverallStatus))
            {
                if (statuses.TryGetValue(study.OverallStatus, out var sc))
                    statuses[study.OverallStatus] = sc + 1;
                else
                    statuses[study.OverallStatus] = 1;
            }
            if (study.Phases != null)
            {
                foreach (var phase in study.Phases)
                {
                    if (!string.IsNullOrEmpty(phase.Phase))
                    {
                        if (phases.TryGetValue(phase.Phase, out var pc))
                            phases[phase.Phase] = pc + 1;
                        else
                            phases[phase.Phase] = 1;
                    }
                }
            }
        }

        object? medicareData = null;
        if (person.MedicareUtilizations is { Count: > 0 })
        {
            var latest = person.MedicareUtilizations
                .OrderByDescending(m => m.DataYear)
                .First();

            Dictionary<string, decimal?>? chronicConditions = null;
            if (!string.IsNullOrEmpty(latest.ChronicConditionsJson))
            {
                chronicConditions = JsonSerializer.Deserialize<Dictionary<string, decimal?>>(latest.ChronicConditionsJson);
            }

            object? topProcedures = null;
            if (person.Procedures is { Count: > 0 })
            {
                var latestYearProcedures = person.Procedures
                    .Where(p => p.DataYear == latest.DataYear)
                    .OrderByDescending(p => p.ServiceCount)
                    .Take(20)
                    .Select(p => new
                    {
                        hcpcsCode = p.HcpcsCode,
                        hcpcsDescription = p.HcpcsDescription,
                        placeOfService = p.PlaceOfService,
                        beneficiaryCount = p.BeneficiaryCount,
                        serviceCount = p.ServiceCount,
                        submittedChargeAmount = p.SubmittedChargeAmount,
                        medicareAllowedAmount = p.MedicareAllowedAmount,
                        medicarePaymentAmount = p.MedicarePaymentAmount
                    })
                    .ToList();

                if (latestYearProcedures.Count > 0)
                    topProcedures = latestYearProcedures;
            }

            medicareData = new
            {
                dataYear = latest.DataYear,
                providerType = latest.ProviderType,
                totalBeneficiaries = latest.TotalBeneficiaries,
                totalServices = latest.TotalServices,
                totalSubmittedCharges = latest.TotalSubmittedCharges,
                totalMedicareAllowedAmount = latest.TotalMedicareAllowedAmount,
                totalMedicarePaymentAmount = latest.TotalMedicarePaymentAmount,
                totalMedicareStandardizedAmount = latest.TotalMedicareStandardizedAmount,
                medicareParticipationIndicator = latest.MedicareParticipationIndicator,
                beneAgeLt65Count = latest.BeneAgeLt65Count,
                beneAge65To74Count = latest.BeneAge65To74Count,
                beneAge75To84Count = latest.BeneAge75To84Count,
                beneAgeGt84Count = latest.BeneAgeGt84Count,
                beneFemaleCount = latest.BeneFemaleCount,
                beneMaleCount = latest.BeneMaleCount,
                beneDualCount = latest.BeneDualCount,
                beneNonDualCount = latest.BeneNonDualCount,
                avgRiskScore = latest.AvgRiskScore,
                medicalServices = latest.MedicalServices,
                drugServices = latest.DrugServices,
                medicalMedicarePayment = latest.MedicalMedicarePayment,
                drugMedicarePayment = latest.DrugMedicarePayment,
                chronicConditions = chronicConditions,
                procedures = topProcedures
            };
        }

        object? openPaymentsData = null;
        if (person.OpenPayments is { Count: > 0 })
        {
            var byType = person.OpenPayments
                .GroupBy(p => p.PaymentType)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        totalPayments = g.Sum(p => p.PaymentAmount ?? 0),
                        count = g.Count(),
                        uniquePayors = g.Select(p => p.PayorName).Where(n => n != null).Distinct().Count(),
                        latestYear = g.Max(p => p.DataYear)
                    });

            var totalAmount = person.OpenPayments.Sum(p => p.PaymentAmount ?? 0);
            var totalCount = person.OpenPayments.Count;
            var uniquePayors = person.OpenPayments.Select(p => p.PayorName).Where(n => n != null).Distinct().Order().ToList();

            var payments = person.OpenPayments
                .OrderByDescending(p => p.DataYear)
                .ThenByDescending(p => p.PaymentAmount)
                .Take(50)
                .Select(p => new
                {
                    dataYear = p.DataYear,
                    paymentType = p.PaymentType,
                    paymentAmount = p.PaymentAmount,
                    paymentDate = p.PaymentDate,
                    payorName = p.PayorName,
                    natureOfPayment = p.NatureOfPayment,
                    formOfPayment = p.FormOfPayment,
                    studyName = p.StudyName,
                    clinicalTrialsId = p.ClinicalTrialsId,
                    contextOfResearch = p.ContextOfResearch,
                    productCategory = p.ProductCategory,
                    productName = p.ProductName
                })
                .ToList();

            openPaymentsData = new
            {
                summary = new
                {
                    totalAmount,
                    totalCount,
                    uniquePayorCount = uniquePayors.Count,
                    uniquePayors,
                    byType
                },
                payments
            };
        }

        object? metricsData = null;
        if (person.Metrics is { Count: > 0 })
        {
            // Get latest Semantic Scholar metrics
            var semanticScholarMetric = person.Metrics
                .Where(m => m.Source == "SemanticScholar")
                .OrderByDescending(m => m.LookupAttemptedAt)
                .FirstOrDefault();

            if (semanticScholarMetric != null)
            {
                metricsData = new
                {
                    source = semanticScholarMetric.Source,
                    hIndex = semanticScholarMetric.HIndex,
                    citationCount = semanticScholarMetric.CitationCount,
                    i10Index = semanticScholarMetric.I10Index,
                    totalPapers = semanticScholarMetric.TotalPapers,
                    externalAuthorId = semanticScholarMetric.ExternalAuthorId,
                    lookupAttemptedAt = semanticScholarMetric.LookupAttemptedAt,
                    lookupResult = semanticScholarMetric.LookupResult
                };
            }
        }

        return new
        {
            uuid = person.Id,
            name = person.FullName,
            orcid = person.Orcid,
            ncbiId = person.NcbiId,
            npi = person.Npi,
            paperCount = person.InvestigatorPapers?.Count ?? 0,
            primaryAffiliation = person.Affiliations?.FirstOrDefault(a => a.IsPrimary)?.InstitutionName,
            affiliations = person.Affiliations?.Select(a => new
            {
                institution = a.InstitutionName,
                department = a.Department,
                city = a.City,
                state = a.State,
                country = a.Country,
                role = a.Role,
                isPrimary = a.IsPrimary
            }).ToList(),
            studyCount = studyList.Count,
            coInvestigators = coInvestigators.OrderBy(x => x).ToList(),
            conditionsFocusAreas = conditions.OrderBy(x => x).ToList(),
            statistics = new
            {
                byStatus = statuses.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
                byPhase = phases.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)
            },
            medicare = medicareData,
            openPayments = openPaymentsData,
            metrics = metricsData
        };
    }
}
