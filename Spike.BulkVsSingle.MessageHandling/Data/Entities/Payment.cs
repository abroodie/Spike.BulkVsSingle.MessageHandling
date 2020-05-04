using System;

namespace Spike.BulkVsSingle.MessageHandling.Data.Entities
{
    public class Payment
    {
        public long Id { get; set; }
        public Guid EventId { get; set; }
        public Guid EarningEventId { get; set; }
        public Guid FundingSourceEventId { get; set; }
        public DateTimeOffset EventTime { get; set; }
        public long Ukprn { get; set; }
        public string LearnerReferenceNumber { get; set; }
        public long LearnerUln { get; set; }
        public string PriceEpisodeIdentifier { get; set; }
        public decimal Amount { get; set; }
        public CollectionPeriod CollectionPeriod { get; set; }
        public byte DeliveryPeriod { get; set; }
        public string LearningAimReference { get; set; }
        public int LearningAimProgrammeType { get; set; }
        public int LearningAimStandardCode { get; set; }
        public int LearningAimFrameworkCode { get; set; }
        public int LearningAimPathwayCode { get; set; }
        public string LearningAimFundingLineType { get; set; }
        public ContractType ContractType { get; set; }
        public TransactionType TransactionType { get; set; }
        public FundingSourceType FundingSource { get; set; }
        public DateTime IlrSubmissionDateTime { get; set; }
        public decimal SfaContributionPercentage { get; set; }
        public long JobId { get; set; }
        public long? AccountId { get; set; }
        public long? TransferSenderAccountId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public DateTime? ActualEndDate { get; set; }
        public byte? CompletionStatus { get; set; }
        public decimal? CompletionAmount { get; set; }
        public decimal? InstalmentAmount { get; set; }
        public short? NumberOfInstalments { get; set; }
        public string AgreementId { get; set; }

        public DateTime? LearningStartDate { get; set; }
        public long? ApprenticeshipId { get; set; }
        public long? ApprenticeshipPriceEpisodeId { get; set; }
        public ApprenticeshipEmployerType ApprenticeshipEmployerType { get; set; }
        public string ReportingAimFundingLineType { get; set; }
    }

    public class CollectionPeriod
    {
        public short AcademicYear { get; set; }
        public byte Period { get; set; }
        public CollectionPeriod Clone()
        {
            return (CollectionPeriod)MemberwiseClone();
        }
    }

    public enum ContractType : byte
    {
        None = byte.MaxValue,
        Act1 = 1,
        Act2 = 2,
    }

    public enum TransactionType : byte
    {
        Learning = 1,
        Completion = 2,
        Balancing = 3,
        First16To18EmployerIncentive = 4,
        First16To18ProviderIncentive = 5,
        Second16To18EmployerIncentive = 6,
        Second16To18ProviderIncentive = 7,
        OnProgramme16To18FrameworkUplift = 8,
        Completion16To18FrameworkUplift = 9,
        Balancing16To18FrameworkUplift = 10,
        FirstDisadvantagePayment = 11,
        SecondDisadvantagePayment = 12,
        OnProgrammeMathsAndEnglish = 13,
        BalancingMathsAndEnglish = 14,
        LearningSupport = 15,
        CareLeaverApprenticePayment = 16,
    }

    public enum FundingSourceType : byte
    {
        Levy = 1,
        CoInvestedSfa = 2,
        CoInvestedEmployer = 3,
        FullyFundedSfa = 4,
        Transfer = 5,
    }

    public enum ApprenticeshipEmployerType : byte
    {
        NonLevy = 0,
        Levy = 1
    }
}
