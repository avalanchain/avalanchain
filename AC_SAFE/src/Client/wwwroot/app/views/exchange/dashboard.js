(function () {
    'use strict';
    var controllerId = 'Exchangedashboard';
    angular.module('avalanchain').controller(controllerId, ['common', '$scope', 'dataservice', 'exchangeservice', '$state','$interval', exchangedashboard]);

    function exchangedashboard(common, $scope, dataservice, exchangeservice, $state, $interval) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'Exchange dashboard';

        //$scope.datayahoo = [];
        //$scope.users = [1, 2, 3, 4];
        //$scope.amount1 = [1000, 2100, 3330, 400];
        //$scope.amount2 = [1100, 2200, 133000, 1400];
        vm.orders = [];
        $scope.ordersPage = 1;
        $scope.coinsPage = 1;

        vm.showSymbol = function (symbol) {
            $state.go('exchange.symbol', {
                symbol: symbol.Symbol
            });
        }

        // vm.inlineData = [34, 43, 43, 35, 44, 32, 44, 52, 25];
        
        // vm.inlineData =[6900.175,6835.344999999999,7461.125,7683.715,7692.65,7800.5599999999995,7882.610000000001,7834.285,7833.01,8017.115,8131.99,8137.19,8121.855,7999.18,7901.415,8007.110000000001,8014.4400000000005,8101.735000000001,7978.2,7967.6900000000005,8000.465,8055.139999999999,8067.025,8104.209999999999,8124.73,8140.895,8288.78,8345.07,8332.355,8327.09,8373.49,8276.425,8109.805,8118.370000000001,8050.61,7973.45,8021.344999999999,8019.3150000000005,8049.92,8042.25,8035.595,8118.4400000000005,8143.54,8118.62,8060.52,7920.17,7920,7956.91,8034.765,8143.75,8122.594999999999,8052.3099999999995,8088.32,8181.0599999999995,8184.155,8222.24,8196.905,8205.97,8249.845000000001,8245.93,8279.76]

                                        
        vm.inlineOptions = {
        type: 'line',
        height: '50px',
        // width: '150px',
        spotRadius: 0,
        lineColor: '#17997f',
        fillColor: '#1ab394'
    };

        function getOrders() {
            return exchangeservice.getOrders().then(function (data) {
                vm.orders = Object.values(data.data);
            });
        }
        function getSymbol() {
            return exchangeservice.mainSymbol().then(function (data) {

                vm.symbol = data.data.Symbol;
            });
        }
        function getSymbols() {
            return exchangeservice.symbols().then(function (data) {
                // vm.symbols = data.data;
                vm.symbols = [];
                vm.symbols.push({Symbol:'BTC'})
                vm.symbols.push({Symbol:'ETH'})
                vm.symbols.push({Symbol:'EOS'})
                vm.symbols.push({Symbol:'XLM'})
                vm.symbols.forEach(element => {
                    element.ImageUrl = '';
                    getHistoday(element)
                });
                
                getPrices()

            });
        }
        function orderCommandsCount() {
            return exchangeservice.orderCommandsCount().then(function (data) {
                vm.orderCommandsCount = data.data;
            });
        }

        function orderEventsCount() {
            return exchangeservice.orderEventsCount().then(function (data) {
                vm.orderEventsCount = data.data;
            });
        }

        function fullOrdersCount() {
            return exchangeservice.fullOrdersCount().then(function (data) {
                vm.fullOrdersCount = data.data;
            });
        }

        $scope.startTimer = function() {
            $scope.Timer = $interval( getData, 10000);
        };

        //TODO: add to service
        $scope.$on("$destroy", function() {
            if (angular.isDefined($scope.Timer)) {
                $interval.cancel($scope.Timer);
            }
        });
        $scope.startTimer();
        function getPrices() {
            var cur = 'USD';
            var list = vm.symbols.map(function(el){
                return el.Symbol;
            }).join();
            // vm.show = false;
            return dataservice.getPrices(cur,list).then(function (data) {
                vm.topPrices = data.data;
                // vm.coinlist.forEach(element => {
                //     element.SortOrder = parseInt(element.SortOrder)
                // });
            });
        }
        
        function getCoinlist() {
            
            // vm.show = false;
            return dataservice.getCoinlist().then(function (data) {
                vm.coinlist = Object.values(data.data.Data);
                vm.coinlist.forEach(element => {
                    element.SortOrder = parseInt(element.SortOrder)
                });
                                
                  vm.coinlist.sort(compare);

                  vm.symbols.forEach(element => {
                    element.ImageUrl = data.data.Data[element.Symbol].ImageUrl;
                    element.FullName = data.data.Data[element.Symbol].FullName;
                });
                // vm.show = true;
                // vm.chartLoading = '';
            });
        }
        function getHistoday(symbol) {
            vm.period = 'histohour';
            vm.show = false;
            return dataservice.getHisto(symbol.Symbol, vm.period).then(function (data) {
                // symbol.histodata = data.data.Data;
                var pos = 0;
                // vm.sampledata1[0].values.splice(vm.histodata.length, vm.sampledata1[0].values.length - vm.histodata.length)
                // data.data.Data.forEach(element => {
                //     element.adjusted =(vm.histodata[pos].low+vm.histodata[pos].high)/2;
                //     pos++;
                // });
                symbol.inlineOptions = angular.copy(vm.inlineOptions);
                symbol.histodata = data.data.Data.map(function(histo,pos) {
                    return ((histo.low + histo.high)/2).toFixed(6);
                  });
            });
        }
        getCoinlist();
        
        getSymbols();
        activate();
        function activate() {
            common.activateController([getData()], controllerId)
                .then(function () { log('Activated Dashboard') });//log('Activated Admin View');
        }


        function getData() {
            getOrders();
            getSymbol();
            orderCommandsCount();
            orderEventsCount();
            fullOrdersCount();
        }

        function compare(a,b) {
            if (a.SortOrder < b.SortOrder)
              return -1;
            if (a.SortOrder > b.SortOrder)
              return 1;
            return 0;
          }
    };



})();