(function () {
    'use strict';
    var controllerId = 'Exchangedashboard';
    angular.module('avalanchain').controller(controllerId, ['common', '$scope', 'dataservice', 'exchangeservice', '$state', exchangedashboard]);

    function exchangedashboard(common, $scope, dataservice, exchangeservice, $state) {
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
        $scope.isEdit = false;

        //dataservice.getPrices().then(function (data) {
        //    if (data.status === 200)
        //        $scope.prices = data.data;
        //});
        setInterval(function updateRandom() {
            if (!$scope.isEdit)
                getData();
        }, 10000);

        vm.showSymbol = function (symbol) {
            $state.go('exchange.symbol', {
                symbol: symbol
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

                vm.symbol = data.data;
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