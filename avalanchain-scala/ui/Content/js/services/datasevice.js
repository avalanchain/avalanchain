(function () {
    'use strict';

    angular.module('avalanchain').filter('pagination', function () {
        return function (input, start) {
            if (input) {
                start = +start; //parse to int
                return input.slice(start);
            }
            return [];
        }
    });
    angular.module('avalanchain').filter('reverse', function () {
        return function (items) {
            return items.slice().reverse();
        };
    });

    angular
        .module('avalanchain')
        .factory('dataservice', dataservice);

    dataservice.$inject = ['$http', '$q', 'common', 'dataProvider', '$filter', '$websocket'];
    /* @ngInject */


    function dataservice($http, $q, common, dataProvider, $filter, $websocket) {
        var getLogFn = common.logger.getLogFn;

      var log = getLogFn('dataservice');
        var service = {
            getData: getData,
            sendPayment: sendPayment,
            getTransactions: getTransactions,
            newAccount: newAccount,
            getAllAccounts: getAllAccounts,
            getYData: getYData,
            addCluster: addCluster,
            addNode: addNode,
            getNodes: getNodes,
            addStream: addStream,
            deleteCluster: deleteCluster,
            clearAllProcesses: clearAllProcesses,
            createGuid: createGuid,
            getQuoka: getQuoka,
            commondata: commondata
    };

        return service;

        function getAllAccounts() {
            var accounts = [];
            var sc = {};
            return dataProvider.get(sc, '/api/account/all', function (data, status) {
                //$scope.GetAllProgresses = data;
            });
        }

        function sendPayment(payment) {
            return $http.post('/api/transaction/submit', payment)
                .success(function (data, status, headers, config) {
                    log("Transaction submited!");//'" + JSON.stringify(data) + "'
                    return data;
                })
                .error(function (data, status, headers, config) {
                    var err = status + ", " + data;
                    log("Request failed: " + err);
                    //$scope.result = "Request failed: " + err;
                    return "error";
                });
        }


        function getTransactions(address) {
            var sc = {};
            return dataProvider.get(sc, '/api/account/get/' + address, function (data, status) {
                //$scope.GetAllProgresses = data;
            });
        }


        function getYData() {
            var url = "http://query.yahooapis.com/v1/public/yql";
            var symbol = '"EURUSD","USDEUR", "USDJPY", "USDGBP", "USDAUD", "USDCHF", "USDSEK", "USDNOK", "USDRUB", "USDTRY", "USDBRL", "USDCAD", "USDCNY", "USDHKD", "USDINR", "USDKRW", "USDMXN", "USDNZD", "USDSGD", "USDZAR"';
            var data = encodeURIComponent("select * from yahoo.finance.xchange where pair in (symbol)");
            data = data.replace("symbol", symbol);
            /*
            http://query.yahooapis.com/v1/public/yql?q=select%20*%20from%20yahoo.finance.quotes%20where%20symbol%20in%20('aapl')&format=json&diagnostics=true&env=http://datatables.org/alltables.env
            */
            var str1 = url.concat("?q=", data);
            str1 = str1.concat("&format=json&env=store://datatables.org/alltableswithkeys"); //http://datatables.org/alltables.env

            var sc = {};
            return dataProvider.get(sc, str1, function (data, status) {
                //$scope.GetAllProgresses = data;
            });

        }

        function addCluster() {

        }

        function getNodes(scope) {
          scope.nodes = [];
          var dataStream = $websocket('ws://localhost:8080/ws/cluster');
          dataStream.onMessage(function(message) {
            scope.nodes.push(message.data);//JSON.parse(message.data)
            if(scope.isUp)
            log("Node up: " + message.data);
            scope.isUp = false;
          });
          // scope.nodes.push({test:'dfd'});

        }
        function addNode() {
          var addNode = $websocket('ws://localhost:8080/ws/newnode');
          addNode.send();

          //   .then(function(data) {
          //   if(data == 200){
          //     log("Node added!");
          //   }
          // });


        }

        function addStream() {

        }

        function deleteCluster() {

        }

        function clearAllProcesses() {

        }

        function newAccount() {
            return $http.post('/api/account/new')
                .success(function (data, status, headers, config) {
                    log("Account created!");//'" + JSON.stringify(data) + "'
                    return data;
                })
                .error(function (data, status, headers, config) {
                    var err = status + ", " + data;
                    log("Request failed: " + err);
                    //$scope.result = "Request failed: " + err;
                    return "error";
                });
        }

        function createGuid() {
            // http://www.ietf.org/rfc/rfc4122.txt
            var s = [];
            var hexDigits = "0123456789abcdef";
            for (var i = 0; i < 36; i++) {
                s[i] = hexDigits.substr(Math.floor(Math.random() * 0x10), 1);
            }
            s[14] = "4"; // bits 12-15 of the time_hi_and_version field to 0010
            s[19] = hexDigits.substr((s[19] & 0x3) | 0x8, 1); // bits 6-7 of the clock_seq_hi_and_reserved to 01
            s[8] = s[13] = s[18] = s[23] = "-";

            var uuid = s.join("");
            return uuid;
        }

        function getQuoka(datayahoo) {
            var data = datayahoo;
            var quoka = 0;
            if (data) {
                for (var i = 0; i < commondata().currencies().length; i++) {

                    if (i === 0) quoka += 0.45;
                    else {
                        quoka += (1 / data[i].Rate) * commondata().percentage()[i];
                    }

                }
                quoka = Math.round(quoka * 1000) / 1000;
            }
            return quoka;
        }

        function commondata() {
            var curr = ["USD", "EUR", "JPY", "GBP", "AUD", "CHF", "SEK", "NOK", "RUB", "TRY", "BRL", "CAD", "CNY", "HKD", "INR", "KRW", "MXN", "NZD", "SGD", "ZAR"];
            var percentage = [45, 17, 12, 6, 4, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0.5, 0.5];
           //return this.currencies = function () {

           //     return curr;
           // }
           //return this.percentage = function () {

           //     return percentage;
           //}

           return {
               currencies: function () {
                   return curr;
               },
               percentage: function () {
                   return percentage;
               }
           };
        }
        function getData() {
            var data = {
                clusters: [
                    {
                        id: '1',
                        nodes: [
                            {
                                id: '1',
                                streams: [
                                    {
                                        id: '1',
                                        data: 3000
                                    },
                                    {
                                        id: '2',
                                        data: 4000
                                    }
                                ]
                            },
                            {
                                id: '2',
                                streams: [
                                    {
                                        id: '1',
                                        data: 3000
                                    },
                                    {
                                        id: '2',
                                        data: 4000
                                    },
                                    {
                                        id: '3',
                                        data: 5000
                                    }
                                ]
                            }
                        ],
                        streams: 5
                    },
                    {
                        id: '2',
                        nodes: [
                            {
                                id: '1',
                                streams: [
                                    {
                                        id: '1',
                                        data: 3300
                                    },
                                    {
                                        id: '2',
                                        data: 4600
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };
            return data;

            //function success(response) {
            //    return response.result;
            //}

            function fail(e) {
                //var msg = 'XHR Failed for getData';
                //logger.error(msg);
                //return exception.catcher(msg)(e);
            }
        }
    }
})();
