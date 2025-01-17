﻿using BusinessLogic.Interfaces.Infrastructure.Repositories;
using BusinessLogic.Interfaces.Services;
using BusinessLogic.Models.DTOs.Outbound;
using BusinessLogic.Models.Entities;
using BusinessLogic.Models.Enums;
using System.Data;

namespace BusinessLogic.Services
{
    public class WeatherReportService : IWeatherReportService
    {
        private readonly ICityRepository cityRepository;
        private readonly IDailyWeatherReportRepository dailyWeatherReportRepository;

        public WeatherReportService(ICityRepository cityRepository, IDailyWeatherReportRepository dailyWeatherReportRepository)
        {
            this.cityRepository = cityRepository;
            this.dailyWeatherReportRepository = dailyWeatherReportRepository;
        }

        public async Task<PeriodWeatherReportDTO> BuildPeriodWeatherReportAsync(string cityName, DateTime fromDate, DateTime toDate, TemperatureUnit requestedTemperatureUnit)
        {
            var noCityDataFound = await CheckIfDataExistsForCityAsync(cityName);
            if (noCityDataFound) throw new ArgumentException($"This service does not have any data for cityName [{cityName}]");

            var datesAreNotOrderedProperly = AreDatesImproperlyOrdered(fromDate, toDate);  
            if(datesAreNotOrderedProperly) throw new ArgumentException($"fromDate cannot be after toDate");

            var dailyReports = await RetrieveDailyWeatherReportsForPeriod(cityName, fromDate, toDate);

            dailyReports = dailyReports.Select(dailyReport => ConvertTemperaturesInDailyWeatherReport(dailyReport, requestedTemperatureUnit)).ToList();

            return BuildPeriodWeatherReport(dailyReports);
        }

        public async Task<bool> CheckIfDataExistsForCityAsync(string cityName)
        {
            return string.IsNullOrEmpty(cityName) || (await cityRepository.Contains(cityName)) == false;
        }

        public static bool AreDatesImproperlyOrdered(DateTime fromDate, DateTime toDate)
        {
            return fromDate > toDate;
        }

        public async Task<List<DailyWeatherReport>> RetrieveDailyWeatherReportsForPeriod(string cityName, DateTime fromDate, DateTime toDate)
        {
            return await dailyWeatherReportRepository.GetDailyWeatherReportsAsync(cityName, fromDate, toDate);
        }

        public static DailyWeatherReport ConvertTemperaturesInDailyWeatherReport(DailyWeatherReport dailyWeatherReport, TemperatureUnit requestedTemperatureUnit)
        {
            dailyWeatherReport.TemperatureMax = ConvertTemperatureToAnotherUnit(dailyWeatherReport.TemperatureMax, dailyWeatherReport.TemperatureUnit, requestedTemperatureUnit);
            dailyWeatherReport.TemperatureAverage = ConvertTemperatureToAnotherUnit(dailyWeatherReport.TemperatureAverage, dailyWeatherReport.TemperatureUnit, requestedTemperatureUnit);
            dailyWeatherReport.TemperatureMin = ConvertTemperatureToAnotherUnit(dailyWeatherReport.TemperatureMin, dailyWeatherReport.TemperatureUnit, requestedTemperatureUnit);
            return dailyWeatherReport;
        }

        public static double ConvertTemperatureToAnotherUnit(double temperature, TemperatureUnit fromUnit, TemperatureUnit toUnit)
        {
            return fromUnit switch
            {
                TemperatureUnit.Fahrenheit => toUnit switch
                {
                    TemperatureUnit.Kelvin => (temperature * (5d / 9d)) - 241.15d,
                    TemperatureUnit.Celsius => (temperature * (5d / 9d)) - 32d,
                    _ => temperature,
                },
                TemperatureUnit.Kelvin => toUnit switch
                {
                    TemperatureUnit.Fahrenheit => (temperature * (9d / 5d)) - 241.15d,
                    TemperatureUnit.Celsius => temperature - 273.15d,
                    _ => temperature,
                },
                _ => toUnit switch
                {
                    TemperatureUnit.Kelvin => temperature + 273.15d,
                    TemperatureUnit.Fahrenheit => (temperature * (9d / 5d)) + 32d,
                    _ => temperature,
                },
            };
        }

        public static PeriodWeatherReportDTO BuildPeriodWeatherReport(List<DailyWeatherReport> dailyWeatherReports)
        {

            var temperatureMax = dailyWeatherReports.Max(dailyReport => dailyReport.TemperatureMax);
            var temperatureAverage = dailyWeatherReports.Average(dailyReport => dailyReport.TemperatureAverage);
            var temperatureMin = dailyWeatherReports.Min(dailyReport => dailyReport.TemperatureMin);
            var cloudCoverAverage = dailyWeatherReports.Average(dailyReport => dailyReport.CloudCoverAverage);
            var numberOfDaysWithPercipitation = dailyWeatherReports.Where(dailyReport => dailyReport.Percipitation > 0).Count();
            var percipitationAverage = dailyWeatherReports.Average(dailyReport => dailyReport.Percipitation);
            var windSpeedAverage = dailyWeatherReports.Average(dailyReport => dailyReport.WindSpeedAverage);
            var temperatureUnit = dailyWeatherReports.First().TemperatureUnit;

            var weatherSummary = WeatherSummary.Fair;

            if (numberOfDaysWithPercipitation >= (dailyWeatherReports.Count / 2) || cloudCoverAverage > 75 || windSpeedAverage >= 10)
            {
                weatherSummary = WeatherSummary.Bad;
            }
            else if (numberOfDaysWithPercipitation < (dailyWeatherReports.Count / 4) && cloudCoverAverage < 25 && windSpeedAverage < 10)
            {
                weatherSummary = WeatherSummary.Great;
            }

            return new PeriodWeatherReportDTO
            {
                TemperatureMax = temperatureMax,
                TemperatureAverage = temperatureAverage,
                TemperatureMin = temperatureMin,
                CloudCoverAverage = cloudCoverAverage,
                NumberOfDaysWithPercipitation = numberOfDaysWithPercipitation,
                PercipitationAverage = percipitationAverage,
                WindSpeedAverage = windSpeedAverage,
                WeatherSummary = weatherSummary,
            };
        }
    }
}
