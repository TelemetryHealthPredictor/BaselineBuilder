using System;
using System.Collections.Generic;
using System.Text;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.Data.ResultObjects;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.Extensions.Options;
using ThpUtils;

namespace BaselineBuilderNew.IO.Output
{
    internal class BaselineCSVWriter
    {
        private readonly BaselineSettings _baselineSettings;

        private QcResult? _qc;
        private R0Result? _r0;
        private CovResult? _cov;
        private CusumSlacksResult? _cusumSlacks;
        private MDPercentilesResult? _mdThresholds;
        private CusumsPercentilesResult? _cusumThresholds;
        private CusumRawPercentilesResult? _cusumRawThresholds;


        public BaselineCSVWriter(IOptions<BaselineSettings> baselineOptions)
        {
            _baselineSettings = baselineOptions.Value;
        }


        public void WriteBaselineCSVFiles()
        {
            ArgumentNullException.ThrowIfNull(_qc);
            ArgumentNullException.ThrowIfNull(_r0);
            ArgumentNullException.ThrowIfNull(_cov);
            ArgumentNullException.ThrowIfNull(_cusumSlacks);
            ArgumentNullException.ThrowIfNull(_mdThresholds);
            ArgumentNullException.ThrowIfNull(_cusumThresholds);
            ArgumentNullException.ThrowIfNull(_cusumRawThresholds);

            string qcPath = Path.Combine(_baselineSettings.BaselineFolderPath, _baselineSettings.NameForQc);
            string r0Path = Path.Combine(_baselineSettings.BaselineFolderPath, _baselineSettings.NameForR0);
            string covPath = Path.Combine(_baselineSettings.BaselineFolderPath, _baselineSettings.NameForCov);
            string cusumSlacksPath = Path.Combine(_baselineSettings.BaselineFolderPath, _baselineSettings.NameForCusumSlacks);
            string mdThresholdsPath =Path.Combine(_baselineSettings.BaselineFolderPath, _baselineSettings.NameForMDThresholds);
            string cusumThresholdsPath = Path.Combine(_baselineSettings.BaselineFolderPath, _baselineSettings.NameForCusumThresholds);
            string cusumRawThresholdsPath = Path.Combine(_baselineSettings.BaselineFolderPath, _baselineSettings.NameForCusumRawThresholds);

            CSVWriter.WriteVector(qcPath, _qc.QcVector);
            CSVWriter.WriteMatrix(r0Path, Matrix<double>.Build.DiagonalOfDiagonalVector(_r0.R0DiagonalVector));
            CSVWriter.WriteMatrix(covPath, _cov.MDCovarianceMatrix);
            CSVWriter.WriteDoubles(cusumSlacksPath, _cusumSlacks.Slacks);
            CSVWriter.WriteDoubles(mdThresholdsPath,_mdThresholds.Thresholds);
            CSVWriter.WriteDoubles(cusumThresholdsPath, _cusumThresholds.ThresholdsForCusums);
            CSVWriter.WriteDoubles(cusumRawThresholdsPath, _cusumRawThresholds.Thresholds);
        }

        public BaselineCSVWriter AddQc(QcResult qc)
        {
            _qc = qc;
            return this;
        }

        public BaselineCSVWriter AddR0(R0Result r0)
        {
            _r0 = r0;
            return this;
        }

        public BaselineCSVWriter AddCov(CovResult cov)
        {
            _cov = cov;
            return this;
        }

        public BaselineCSVWriter AddCusumSlacks(CusumSlacksResult cusumSlacks)
        {
            _cusumSlacks = cusumSlacks;
            return this;
        }

        public BaselineCSVWriter AddMDThresholds(MDPercentilesResult mdThresholds)
        {
            _mdThresholds = mdThresholds;
            return this;
        }

        public BaselineCSVWriter AddCusumThresholds(CusumsPercentilesResult cusumThresholds)
        {
            _cusumThresholds = cusumThresholds;
            return this;
        }

        public BaselineCSVWriter AddCusumRawThresholds(CusumRawPercentilesResult cusumRawThresholds)
        {
            _cusumRawThresholds = cusumRawThresholds;
            return this;
        }
    }
}
