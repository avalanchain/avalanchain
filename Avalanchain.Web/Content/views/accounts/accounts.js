(function () {
    'use strict';
    var controllerId = 'accounts';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', accounts]);

    function accounts(common, dataservice, $scope) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'accounts';
        vm.helloText = 'Welcome in Avalanchain';
        vm.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';
        $scope.searchAccounts = '';
        $scope.valinside = false;   

        $scope.showAccount= function (value) {
            $scope.current = value;
            $scope.valinside = true;
        };

        //$scope.pagination = function () {
        //    return function (input, start) {
        //        if (input) {
        //            start = +start; //parse to int
        //            return input.slice(start);
        //        }
        //        return [];
        //    }
        //}

        $scope.clean = function() {
            $scope.searchAccounts = '';
        }
        function addStatus(data) {
            if (data) {
                for (var i = 0; i < data.length; i++) {
                    if (data[i].status === 'active') {
                        data[i]["navigation"] = 'label-primary';
                    } else {
                        data[i]["navigation"] = 'label-deafault';
                    }
                }
            }
            return data;
        }


        activate();

        function activate() {
            common.activateController([getAccounts()], controllerId)
                .then(function () { log('Activated Accounts') });//log('Activated Admin View');
        }


        function getAccounts() {
            return dataservice.getAllAccounts().then(function (data) {
                $scope.accounts = addStatus(data.data);
                //$scope.accounts = addStatus(dataservice.getAccounts());
                $scope.maxSize = 5;
                $scope.totalItems = $scope.accounts.length;
                $scope.currentPage = 1;
                //$scope.$digest();
            });
            
        }

        
        
    };
   

})();
