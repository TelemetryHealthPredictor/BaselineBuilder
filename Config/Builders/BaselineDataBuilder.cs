using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using BaselineBuilderNew.Config.Settings;
using BaselineBuilderNew.Data.BaselineData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpCompress.Common;
using ThpUtils;
using ThpUtils.Naming;

namespace BaselineBuilderNew.Config.Builders
{
    internal class BaselineDataBuilder
    {
        public static CusumData BuildCusumData(IServiceProvider provider)
        {
            BaselineSettings settings = provider.GetRequiredService<IOptions<BaselineSettings>>().Value;

            string key = settings.CusumDriftSizeKey;

            return new CusumData()
            {
                DriftSize = GetElement(provider, key)
            };
        }

        public static KalmanData BuildKalmanData(IServiceProvider provider)
        {
            BaselineSettings settings = provider.GetRequiredService<IOptions<BaselineSettings>>().Value;

            string key = settings.ForgettingFactorKey;

            double forgettingFactor = GetElement(provider, key);

            return new KalmanData()
            {
                ForgettingFactor = forgettingFactor
            };
        }

        private static double GetElement(IServiceProvider provider, string key)
        {
            BaselineSettings settings = provider.GetRequiredService<IOptions<BaselineSettings>>().Value;

            string filePath = Path.Combine(settings.BaselineFolderPath, settings.ScalarsFileName);
            string json = File.ReadAllText(filePath);

            using JsonDocument scalarsDoc = JsonDocument.Parse(json);

            if (!scalarsDoc.RootElement.TryGetProperty(key, out JsonElement element))
            {
                throw new KeyNotFoundException($"Key '{key}' was not found in '{filePath}'.");
            }

            return element.GetDouble();
        }
    }
}
