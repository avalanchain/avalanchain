(function () {
    'use strict';
    var controllerId = 'QuokaTrader';
    angular.module('avalanchain').controller(controllerId, ['common', '$scope', 'dataservice', QuokaTrader]);

    function QuokaTrader(common, $scope, dataservice) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        this.info = 'quoka trader';
        this.helloText = 'Welcome in avalanchain';
        this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';
        $scope.datayahoo = [];
        $scope.quoka = 0;
        $scope.users = [1, 2, 3, 4];
        $scope.amount1 = [1000, 2100, 3330, 400];
        $scope.amount2 = [1100, 2200, 133000, 1400];
        $scope.transactions = [];
        $scope.transactionPage = 1;
        $scope.isEdit = false;
        setInterval(function updateRandom() {
            if (!$scope.isEdit)
                getData();
        }, 3000);

        var masteruser = {};
        $scope.edit = function (user) {
            masteruser = angular.copy(user);
            $scope.changeview(user);
        }
        $scope.save = function (user) {
            $scope.changeview(user);
        }
        $scope.cancel = function (user) {
            user.max = masteruser.max;
            user.min = masteruser.min;
            $scope.changeview(user);
        }
        $scope.changeview = function (user) {
            user.isEdit = !user.isEdit;
            $scope.isEdit = !$scope.isEdit;
        }

        var dataprev = [];
        function addAmount(data) {
            var isFirst = $scope.users[0]["max"] >= 0 ? false : true;
            if (data) {

                for (var i = 0; i < $scope.amount2.length; i++) {
                    if (dataprev.length === 0) dataprev = data;

                    if (dataprev[i].Rate > data[i].Rate) {
                        data[i]["status"] = 'text-danger';
                        data[i]["navigation"] = 'fa fa-play fa-rotate-90';
                    } else if (dataprev[i].Rate < data[i].Rate) {
                        data[i]["status"] = 'text-navy';
                        data[i]["navigation"] = 'fa fa-play fa-rotate-270';
                    } else {
                        data[i]["status"] = '';
                        data[i]["navigation"] = 'fa fa-pause';
                    }
                    data[i]["amount1"] = $scope.amount1[i];
                    data[i]["amount2"] = $scope.amount2[i];
                    if (isFirst) {
                        var ask = parseFloat(data[i].Ask);
                        var bid = parseFloat(data[i].Bid);
                        data[i]["max"] = ask + (ask - bid);
                        data[i]["min"] = ask - (ask - bid);
                    } else {
                        data[i]["max"] = $scope.users[i].max;
                        data[i]["min"] = $scope.users[i].min;;
                    }
                    data[i]["from"] = data[i].Name.split('/')[0];
                    data[i]["to"] = data[i].Name.split('/')[1];
                    data[i] = action(data[i]);
                    $scope.amount1[i] = data[i].amount1;
                    $scope.amount2[i] = data[i].amount2;
                    data[i]["quoka"] = totalQuoka(data[i], i);
                    data[i]["isEdit"] = false;
                }

                function totalQuoka(user, pos) {
                    var quoka = 0;
                    if (pos === 0) {
                        quoka = (user.amount1 / user.Rate) / $scope.quoka + user.amount2 / $scope.quoka;
                    } else {
                        quoka = (user.amount2 / user.Rate) / $scope.quoka + user.amount1 / $scope.quoka;
                    }
                    return quoka;
                }
            }
            dataprev = data;
            return data;
        }

        function action(user) {
            var sell = 40;
            var buy = 40;
            if (user.max !== 0 && user.max > user.Ask && user.amount1 >= sell) {
                user.amount1 = user.amount1 - sell;
                user.amount2 = user.amount2 + sell * user.Ask;

                $scope.transactions.unshift({
                    from: 'Sell ' + user.Name.split('/')[0],
                    amountFrom: sell,
                    name: user.Name,
                    to: 'Buy ' + user.Name.split('/')[1],
                    amountTo: sell * user.Ask,
                    quokas: (sell * user.Ask) / $scope.quoka
                });
                if ($scope.transactions.length > 100) $scope.transactions.pop();
            }

            if (user.min !== 0 && user.min <= user.Bid && user.amount2 >= buy * user.Bid) {
                user.amount2 = user.amount2 - buy * user.Bid;
                user.amount1 = user.amount1 + buy;

                $scope.transactions.unshift({
                    from: 'Sell ' + user.Name.split('/')[1],
                    amountFrom: buy * user.Bid,
                    name: user.Name,
                    to: 'Buy ' + user.Name.split('/')[0],
                    amountTo: buy,
                    quokas: (buy) / $scope.quoka
                });
                if ($scope.transactions.length > 100) $scope.transactions.pop();
            }
            return user;
        }
        function getData() {
            //return dataservice.getYData().then(function (data) {
            //    $scope.quoka = dataservice.getQuoka(data.data.query.results.rate);
            //    $scope.users = addAmount(data.data.query.results.rate);
            //});
            return dataservice.getYData().then(function (data) {
                if (data.length > 0) {
                    $scope.quoka = dataservice.getQuoka(data);
                    $scope.users = addAmount(data);
                }
            });
        }
        activate();
        function activate() {
            common.activateController([getData()], controllerId)
                .then(function () { log('Activated Quoka Trader') });//log('Activated Admin View');
        }
    };


})();