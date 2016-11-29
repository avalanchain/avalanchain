/// <reference path="create_account.html" />
(function () {
    'use strict';
    var controllerId = 'accounts';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$filter', '$uibModal', '$rootScope', '$state', '$interval', accounts]);

    function accounts(common, dataservice, $scope, $filter, $uibModal, $rootScope, $state, $interval) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        $scope.searchAccounts = '';
        // $scope.isAccountPage = false;
        $scope.transactions = [];
        $scope.current = {};
        $scope.payment = {
            fromAcc: {},
            toAcc: {}
        };
        $scope.maxSize = 5;
        $scope.totalItems = [];
        $scope.currentPage = 1;
        $scope.transactionPage = 1;
        $scope.transactions = [];
        vm.accounts = [];
        // dataservice.getAccounts(vm);
        $scope.openModal = function () {
            var m = new Mnemonic(96);
            $rootScope.modal = {};
            $rootScope.modal.password = m.toWords().join(' ');;
            $rootScope.modal.hexPass = m.toHex();
            $rootScope.modal.guid = dataservice.getId();
            $rootScope.modal.ok =function () {
                return dataservice.newAccount().then(function (data) {
                    // $rootScope.$emit('updateAccounts');
                    return 200;
                });
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/accounts/create_account.html',
                controller: modalCtrl
            });
        };
        //$scope.showModal = false;
        //$scope.toggleModal = function () {
        //    $scope.showModal = !$scope.showModal;
        //};
        // $rootScope.$on('updateAccounts', function () {
        //     getAccounts();
        // });

        $scope.getTransactions = function (address) {

            $scope.current = $filter('filter')($scope.accounts, {
                ref: { 'address': address }
            }, true)[0];//$scope.current
            // return dataservice.getTransactions(address).then(function(data) {
            //     $scope.transactions = data.data.fields[0].transactions;
            //     $scope.current.totalTransactions = $scope.transactions.length;
            // });
        }

        $scope.showAccount = function(account) {
            $state.go('index.account', {
                accountId: account.ref.address
            });
        }

        $scope.showWallet = function(account) {
            $state.go('index.wallet', {
                accountId: account.ref.address
            });
        }
        // $scope.showAccount = function (value) {
        //     var currentTransactionPage = $scope.transactionPage;
        //     $scope.current = value;
        //     $scope.transactionPage = $scope.current.ref.address != value.ref.address ? 1 : currentTransactionPage;
        //
        //     $scope.isAccountPage = true;
        //     $scope.payment.fromAcc = value.ref;
        //     //return $scope.getTransactions($scope.payment.fromAcc.address);
        // };

        //$scope.newAccount = function() {
        //    dataservice.newAccount().then(function (data) {
        //        getAccounts();
        //    });
        //}

        $scope.sendPayment = function () {
            dataservice.sendPayment($scope.payment).then(function (data) {
                $scope.getTransactions($scope.current.ref.address);
                //getAccounts();
            });
        }

        $scope.clean = function() {
            $scope.searchAccounts = '';
        }

        $scope.refresh = function () {
            getAccounts();
        }

        vm.startTimer = function() {
            vm.Timer = $interval(getAccounts, 500);
        };

        //TODO: add to service
        $scope.$on("$destroy", function() {
            if (angular.isDefined(vm.Timer)) {
                $interval.cancel(vm.Timer);
            }
        });

        function getAccounts() {
            dataservice.getAccs().then(function(data) {
                vm.users = data.data;
                for (var pr in data.data) {
                    var acc = data.data[pr];
                        var property = '';
                        for (var prop in acc.status) {
                            if (acc.status.hasOwnProperty(prop)){
                                property =  prop;
                                break;
                            }

                        }
                        vm.accounts.push({
                            name: acc.account.accountId.replace(/-/gi, ''),
                            publicKey: acc.account.accountId.replace(/-/gi, ''),
                            balance: acc.balance,
                            status: property,
                            signed: true,
                            expired: acc.account.expire,
                            ref: {
                                address: acc.account.accountId.replace(/-/gi, '')
                            }
                        });
                }
            });
        }

        vm.startTimer()

        // setInterval(function updateRandom() {
        //
        //     if ($scope.isAccountPage) {
        //         $scope.getTransactions($scope.current.ref.address);
        //     }
        //         getAccounts();
        //
        //
        // }, 3000);

        function addStatus(data) {
            if (data) {
                for (var i = 0; i < data.length; i++) {
                    if (data[i].status === 1) {
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
            common.activateController([], controllerId)
                .then(function () { log('Activated Accounts') });//log('Activated Admin View');
        }






    };


})();
