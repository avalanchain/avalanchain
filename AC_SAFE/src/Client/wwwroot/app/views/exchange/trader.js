(function () {
    'use strict';
    var controllerId = 'Trader';
    angular.module('avalanchain').controller(controllerId, ['common', '$scope', 'dataservice','exchangeservice', trader]);

    function trader(common, $scope, dataservice, exchangeservice) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'trader';

        $scope.datayahoo = [];
        $scope.users = [1, 2, 3, 4];
        $scope.amount1 = [1000, 2100, 3330, 400];
        $scope.amount2 = [1100, 2200, 133000, 1400];
        $scope.transactions = [];
        $scope.transactionPage = 1;
        $scope.isEdit = false;

        dataservice.getPrices().then(function (data) {
            if(data.status==200)
                $scope.prices = data.data;
        });
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
                    }
                    else if (dataprev[i].Rate < data[i].Rate) {
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
                    data[i]["isEdit"] = false;
                    data[i] = action(data[i]);
                    $scope.amount1[i] = data[i].amount1;
                    $scope.amount2[i] = data[i].amount2;
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
                    time: user.Time
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
                    time: user.Time
                });
                if ($scope.transactions.length > 100) $scope.transactions.pop();
            }
            return user;
        }

        function getData() {
            return dataservice.getYData().then(function (data) {
                if (data.length > 0) {
                    //var dt = dataservice.mapping(data.rate, $scope);
                    $scope.users = addAmount(data);
                }
            });
        }
        activate();
        function activate() {
            common.activateController([getData()], controllerId)
                .then(function () { log('Activated trader') });//log('Activated Admin View');
        }

       
        
    };

    
    

})();