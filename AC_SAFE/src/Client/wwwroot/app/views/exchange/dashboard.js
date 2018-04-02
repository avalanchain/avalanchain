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

        vm.showSymbol = function (symbol) {
            $state.go('exchange.symbol', {
                symbol: symbol.Symbol
            });
        }

        
        //var masteruser = {};
        //$scope.edit = function (user) {
        //    masteruser = angular.copy(user);
        //    $scope.changeview(user);
        //}
        //$scope.save = function (user) {
        //    $scope.changeview(user);
        //}
        //$scope.cancel = function (user) {
        //    user.max = masteruser.max;
        //    user.min = masteruser.min;
        //    $scope.changeview(user);
        //}
        //$scope.changeview = function (user) {
        //    user.isEdit = !user.isEdit;
        //    $scope.isEdit = !$scope.isEdit;
        //}
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

                vm.symbols = data.data;
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
        activate();
        function activate() {
            common.activateController([getData()], controllerId)
                .then(function () { log('Activated Dashboard') });//log('Activated Admin View');
        }


        function getData() {
            getOrders();
            getSymbol();
            getSymbols();
            orderCommandsCount();
            orderEventsCount();
            fullOrdersCount();
        }
    };



})();