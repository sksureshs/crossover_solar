using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using CrossSolar.Controllers;
using CrossSolar.Domain;
using CrossSolar.Models;
using CrossSolar.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Moq;
using Xunit;

namespace CrossSolar.Tests.Controller
{
    public class AnalyticsControllerTests
    {
        private readonly AnalyticsController _analyticsController;

        private IPanelRepository _panelRepository;

        private IAnalyticsRepository _oneHrElectricityRepository;

        private OneHourElectricity[] _oneHrElectricities;

        public AnalyticsControllerTests()
        {
            SetUpMockRepositories();
            _analyticsController = new AnalyticsController(_oneHrElectricityRepository, _panelRepository);
        }

        private void SetUpMockRepositories()
        {
            var panels = new[]
            {
                new Panel
                {
                    Brand = "TikTak",
                    Latitude = 22.345678,
                    Longitude = 58.7655432,
                    Serial = "SSSS22225555TTTT"
                }
            }.AsQueryable();

            var mockSet = new Mock<DbSet<Panel>>();

            mockSet.As<IAsyncEnumerable<Panel>>()
                .Setup(m => m.GetEnumerator())
                .Returns(new TestAsyncEnumerator<Panel>(panels.GetEnumerator()));

            mockSet.As<IQueryable<Panel>>()
                .Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<Panel>(panels.Provider));

            mockSet.As<IQueryable<Panel>>().Setup(m => m.Expression).Returns(panels.Expression);
            mockSet.As<IQueryable<Panel>>().Setup(m => m.ElementType).Returns(panels.ElementType);
            mockSet.As<IQueryable<Panel>>().Setup(m => m.GetEnumerator()).Returns(() => panels.GetEnumerator());

            var contextOptions = new DbContextOptions<CrossSolarDbContext>();
            var mockContext = new Mock<CrossSolarDbContext>(contextOptions);
            mockContext.Setup(c => c.Set<Panel>()).Returns(mockSet.Object);

            _panelRepository = new PanelRepository(mockContext.Object);

            _oneHrElectricities = new[]
            {
                 new OneHourElectricity
                {
                    Id = 1,
                    PanelId = "SSSS22225555TTTT",
                    DateTime = DateTime.Now,
                    KiloWatt = 240
                },
                new OneHourElectricity
                {
                    Id = 2,
                    PanelId = "SSSS22225555TTTT",
                    DateTime = DateTime.Now.AddDays(1),
                    KiloWatt = 2400
                }
            };

            var oneHrElectricitiesQueryable =_oneHrElectricities.AsQueryable();

            var analyticsMockSet = new Mock<DbSet<OneHourElectricity>>();

            analyticsMockSet.As<IAsyncEnumerable<OneHourElectricity>>()
                .Setup(m => m.GetEnumerator())
                .Returns(new TestAsyncEnumerator<OneHourElectricity>(oneHrElectricitiesQueryable.GetEnumerator()));

            analyticsMockSet.As<IQueryable<Panel>>()
                .Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<OneHourElectricity>(panels.Provider));

            analyticsMockSet.As<IQueryable<OneHourElectricity>>().Setup(m => m.Expression).Returns(oneHrElectricitiesQueryable.Expression);
            analyticsMockSet.As<IQueryable<OneHourElectricity>>().Setup(m => m.ElementType).Returns(oneHrElectricitiesQueryable.ElementType);
            analyticsMockSet.As<IQueryable<OneHourElectricity>>().Setup(m => m.GetEnumerator()).Returns(() => oneHrElectricitiesQueryable.GetEnumerator());
            
            mockContext.Setup(c => c.Set<OneHourElectricity>()).Returns(analyticsMockSet.Object);

            _oneHrElectricityRepository = new AnalyticsRepository(mockContext.Object);
        }

        [Fact]
        public async Task GetPanelAnalytics_WithValidPanelIdShouldReturnList()
        {
           
            var result = await _analyticsController.Get("SSSS22225555TTTT");

            // Assert
            Assert.NotNull(result);

            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);
            Assert.Equal(200, okResult.StatusCode);
            Assert.True(((OneHourElectricityListModel)okResult.Value).OneHourElectricitys.ToList().Count() == 2);
        }

        [Fact]
        public async Task GetPanelAnalytics_InvalidPanelIdThrowNotFoundError()
        {
            var result = await _analyticsController.Get("ASAS3434DFDF1234");

            Assert.NotNull(result);

            var notFoundResult = result as NotFoundResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task DayResultsAnalytics_WithValidPanelIdShouldReturnList()
        {
            var result = await _analyticsController.DayResults("SSSS22225555TTTT");

            Assert.NotNull(result);

            var okResult = result as OkObjectResult;
            Assert.NotNull(okResult);
            Assert.Equal(200, okResult.StatusCode);
            var dayResultEnumerable = okResult.Value as IEnumerable<OneDayElectricityModel>;
            Assert.NotNull(dayResultEnumerable);
            var dayResult = dayResultEnumerable.ToList();
            Assert.NotNull(dayResult);
            Assert.True(dayResult.Count == 2);
            Assert.True(dayResult[0].DateTime.Date == DateTime.Now.Date);
            Assert.True(dayResult[0].Average == (double)_oneHrElectricities[0].KiloWatt/24d);
            Assert.True(dayResult[0].Minimum == (double)_oneHrElectricities[0].KiloWatt);
            Assert.True(dayResult[0].Maximum == (double)_oneHrElectricities[0].KiloWatt);
            Assert.True(dayResult[0].Sum == (double)_oneHrElectricities[0].KiloWatt);
        }

        [Fact]
        public async Task DayResultsAnalytics_InvalidPanelIdThrowNotFoundError()
        {
            var result = await _analyticsController.DayResults("ASAS3434DFDF1234");

            Assert.NotNull(result);
            var notFoundResult = result as NotFoundResult;
            Assert.NotNull(notFoundResult);
            Assert.Equal(404, notFoundResult.StatusCode);
        }

        [Fact]
        public async Task Post_ShouldCreateAnalysticsModel()
        {
            var panelId = "SSSS22225555TTYY";
            var oneHrEleModel = new OneHourElectricityModel
            {
                Id = 1,
                KiloWatt = 1240,
                DateTime = DateTime.Now
            };

            var result = await _analyticsController.Post(panelId, oneHrEleModel);

            // Assert
            Assert.NotNull(result);

            var createdResult = result as CreatedResult;
            Assert.NotNull(createdResult);
            Assert.Equal(201, createdResult.StatusCode);
        }

    
    }

    internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TestAsyncEnumerable<TElement>(expression);
        }

        public object Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression expression)
        {
            return new TestAsyncEnumerable<TResult>(expression);
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute<TResult>(expression));
        }
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable)
            : base(enumerable)
        { }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        { }

        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider
        {
            get { return new TestAsyncQueryProvider<T>(this); }
        }
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public T Current
        {
            get { return _inner.Current; }
        }

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(_inner.MoveNext());
        }
    }
}
