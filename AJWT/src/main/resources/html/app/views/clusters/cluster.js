/// <reference path="create_account.html" />
(function() {
    'use strict';
    var controllerId = 'cluster';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$filter', '$uibModal', '$rootScope', '$stateParams', '$interval', cluster]);

    function cluster(common, dataservice, $scope, $filter, $uibModal, $rootScope, $stateParams, $interval) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.pieData = [];
        vm.maxSize = 5;
        vm.totalItems = [];
        vm.currentPage = 1;
        vm.transactionPage = 1;

        var clusterId = $stateParams.clusterId;
        dataservice.getData().then(function(data) {
            vm.clusters = data.clusters;
            vm.nodes = data.nodes;
            vm.streams = data.streams;
            vm.cluster = vm.clusters.filter(function(cluster) {
                return cluster.id === clusterId;
            })[0];
            vm.getTransactions();
        });
        vm.getTransactions = function() {
            dataservice.getData().then(function(data) {
                vm.transactions = data.transactions;
                // .filter(function(transaction) {
                //     return transaction.node === nodeId;
                // });

                // var currentTransactionPage = vm.transactionPage;
                // $scope.payment.fromAcc = vm.current.ref;
                vm.goupedTypes = {};
                for (var i = 0; i < vm.nodes.length; ++i) {
                    var obj = vm.nodes[i];
                    if (vm.goupedTypes[obj.name] === undefined){
                      vm.goupedTypes[obj.name] = [obj.name];
                    }
                    else{
                      vm.goupedTypes[obj.name].push(obj.name);
                    }

                }
                var t = 0;
                var colors = ["#1ab394", '#79d2c0','#d3d3d3', '#bababa', '#bababa']
                for (var propertyName in vm.goupedTypes) {
                    vm.pieData.push({
                        label: 'Node: ' + propertyName,
                        data: vm.goupedTypes[propertyName].length,
                        color: colors[t]
                    })
                    t++;
                }

            });
        }

        vm.pieOptions = {
            series: {
                pie: {
                    show: true
                }
            },
            grid: {
                hoverable: true
            },
            tooltip: true,
            tooltipOpts: {
                content: "%p.0%, %s", // show percentages, rounding to 2 decimal places
                shifts: {
                    x: 20,
                    y: 0
                },
                defaultTheme: false
            }
        };

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function() {}); //log('Activated Admin View');
        }

    };


})();
